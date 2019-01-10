using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AzureStorage.Tables;
using Common.Log;
using Lykke.Common.ApiLibrary.Middleware;
using Lykke.Common.ApiLibrary.Swagger;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Models;
using Lykke.Job.TransactionHandler.Modules;
using Lykke.Job.TransactionHandler.Queues;
using Lykke.Job.TransactionHandler.Services;
using Lykke.JobTriggers.Extenstions;
using Lykke.Logs;
using Lykke.SettingsReader;
using Lykke.SlackNotification.AzureQueue;
using Lykke.JobTriggers.Triggers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lykke.Job.TransactionHandler
{
    public class Startup
    {
        public IHostingEnvironment Environment { get; }
        public IContainer ApplicationContainer { get; set; }
        public IConfigurationRoot Configuration { get; }
        public ILog Log { get; private set; }
        private TriggerHost _triggerHost;
        private Task _triggerHostTask;

        private readonly IEnumerable<Type> _subscribers = new List<Type>
        {
            typeof(CashInOutQueue),
            typeof(EthereumEventsQueue),
            typeof(LimitTradeQueue),
            typeof(TradeQueue),
            typeof(TransferQueue)
        };

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
            Environment = env;
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            try
            {
                services.AddMvc()
                        .AddJsonOptions(options =>
                        {
                            options.SerializerSettings.ContractResolver =
                                new Newtonsoft.Json.Serialization.DefaultContractResolver();
                        });

                services.AddSwaggerGen(options =>
                {
                    options.DefaultLykkeConfiguration("v1", "TransactionHandler API");
                });

                var builder = new ContainerBuilder();
                var appSettings = Configuration.LoadSettings<AppSettings>();
                Log = CreateLogWithSlack(services, appSettings);

                builder.RegisterModule(new JobModule(appSettings.CurrentValue, appSettings.Nested(x => x.TransactionHandlerJob.Db), Log));
                builder.RegisterModule(new CqrsModule(appSettings, Log));
                builder.RegisterModule(new MatchingEngineModule(appSettings.Nested(x => x.MatchingEngineClient), Log));

                if (string.IsNullOrWhiteSpace(appSettings.CurrentValue.TransactionHandlerJob.Db.BitCoinQueueConnectionString))
                {
                    builder.AddTriggers();
                }
                else
                {
                    builder.AddTriggers(pool =>
                    {
                        pool.AddDefaultConnection(appSettings.ConnectionString(x => x.TransactionHandlerJob.Db.BitCoinQueueConnectionString));
                    });
                }

                builder.Populate(services);

                ApplicationContainer = builder.Build();

                return new AutofacServiceProvider(ApplicationContainer);
            }
            catch (Exception ex)
            {
                Log?.WriteFatalError(nameof(Startup), nameof(ConfigureServices), ex);
                throw;
            }
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime appLifetime)
        {
            try
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }

                app.UseLykkeMiddleware("TransactionHandler", ex => new ErrorResponse { ErrorMessage = "Technical problem" });

                app.UseMvc();
                app.UseSwagger();
                app.UseSwaggerUI(x =>
                {
                    x.RoutePrefix = "swagger/ui";
                    x.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                });
                app.UseStaticFiles();

                appLifetime.ApplicationStarted.Register(() => StartApplication().GetAwaiter().GetResult());
                appLifetime.ApplicationStopping.Register(() => StopApplication().GetAwaiter().GetResult());
                appLifetime.ApplicationStopped.Register(CleanUp);
            }
            catch (Exception ex)
            {
                Log?.WriteFatalError(nameof(Startup), nameof(ConfigureServices), ex);
                throw;
            }
        }

        private async Task StartApplication()
        {
            try
            {
                // NOTE: Job not yet receive and process IsAlive requests here

                var cqrs = ApplicationContainer.Resolve<ICqrsEngine>();
                cqrs.StartSubscribers();
                cqrs.StartProcesses();

                StartSubscribers();

                _triggerHost = new TriggerHost(new AutofacServiceProvider(ApplicationContainer));
                _triggerHostTask = _triggerHost.Start();

                Log.WriteMonitor("", "", "Started");
            }
            catch (Exception ex)
            {
                Log.WriteFatalError(nameof(Startup), nameof(StartApplication), ex);
                throw;
            }
        }

        private async Task StopApplication()
        {
            try
            {
                // NOTE: Job still can receive and process IsAlive requests here, so take care about it if you add logic here.

                StopSubscribers();

                _triggerHost?.Cancel();
                await _triggerHostTask;
            }
            catch (Exception ex)
            {
                Log?.WriteFatalError(nameof(Startup), nameof(StopApplication), ex);
                throw;
            }
        }

        private void CleanUp()
        {
            try
            {
                // NOTE: Job can't receive and process IsAlive requests here, so you can destroy all resources

                Log?.WriteMonitor("", "", "Terminating");

                ApplicationContainer.Dispose();
            }
            catch (Exception ex)
            {
                if (Log != null)
                {
                    Log.WriteFatalError(nameof(Startup), nameof(CleanUp), ex);
                    (Log as IDisposable)?.Dispose();
                }
                throw;
            }
        }

        private static ILog CreateLogWithSlack(IServiceCollection services, IReloadingManager<AppSettings> settings)
        {
            var consoleLogger = new LogToConsole();
            var aggregateLogger = new AggregateLogger();

            aggregateLogger.AddLog(consoleLogger);

            // Creating slack notification service, which logs own azure queue processing messages to aggregate log
            var slackService = services.UseSlackNotificationsSenderViaAzureQueue(new AzureQueueIntegration.AzureQueueSettings
            {
                ConnectionString = settings.CurrentValue.SlackNotifications.AzureQueue.ConnectionString,
                QueueName = settings.CurrentValue.SlackNotifications.AzureQueue.QueueName
            }, aggregateLogger);

            var dbLogConnectionStringManager = settings.Nested(x => x.TransactionHandlerJob.Db.LogsConnString);
            var dbLogConnectionString = dbLogConnectionStringManager.CurrentValue;

            // Creating azure storage logger, which logs own messages to console log
            if (!string.IsNullOrEmpty(dbLogConnectionString) && !(dbLogConnectionString.StartsWith("${") && dbLogConnectionString.EndsWith("}")))
            {
                var persistenceManager = new LykkeLogToAzureStoragePersistenceManager(
                    AzureTableStorage<LogEntity>.Create(dbLogConnectionStringManager, "TransactionHandlerLog", consoleLogger),
                    consoleLogger);

                var slackNotificationsManager = new LykkeLogToAzureSlackNotificationsManager(slackService, consoleLogger);

                var azureStorageLogger = new LykkeLogToAzureStorage(
                    persistenceManager,
                    slackNotificationsManager,
                    consoleLogger);

                azureStorageLogger.Start();

                aggregateLogger.AddLog(azureStorageLogger);
            }

            return aggregateLogger;
        }

        private void StartSubscribers()
        {
            foreach (var subscriber in _subscribers)
            {
                ((IQueueSubscriber)ApplicationContainer.Resolve(subscriber)).Start();
            }
        }

        private void StopSubscribers()
        {
            foreach (var subscriber in _subscribers)
            {
                ((IQueueSubscriber)ApplicationContainer.Resolve(subscriber)).Stop();
            }
        }
    }
}
