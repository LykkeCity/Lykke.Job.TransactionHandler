using System;
using System.Collections.Generic;
using Autofac;
using AzureStorage.Queue;
using Common.Log;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.RabbitMq;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Handlers;
using Lykke.Job.TransactionHandler.Projections;
using Lykke.Job.TransactionHandler.Sagas;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Messaging;
using Lykke.SettingsReader;

namespace Lykke.Job.TransactionHandler.Modules
{
    public class CqrsModule : Module
    {
        private readonly AppSettings _settings;
        private readonly ILog _log;
        private readonly IReloadingManager<AppSettings.DbSettings> _dbSettingsManager;

        public CqrsModule(IReloadingManager<AppSettings> settingsManager, ILog log)
        {
            _dbSettingsManager = settingsManager.Nested(x => x.TransactionHandlerJob.Db);
            _settings = settingsManager.CurrentValue;
            _log = log;
        }

        protected override void Load(ContainerBuilder builder)
        {
            if (_settings.TransactionHandlerJob.ChaosKitty != null)
            {
                ChaosKitty.StateOfChaos = _settings.TransactionHandlerJob.ChaosKitty.StateOfChaos;
            }

            InitSerializer();

            builder.Register(context => new AutofacDependencyResolver(context)).As<IDependencyResolver>().SingleInstance();

            var rabbitMqSettings = new RabbitMQ.Client.ConnectionFactory { Uri = _settings.TransactionHandlerJob.SagasRabbitMqConnStr };
#if DEBUG
            var virtualHost = "/debug";
            var messagingEngine = new MessagingEngine(_log,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"RabbitMq", new TransportInfo(rabbitMqSettings.Endpoint + virtualHost, rabbitMqSettings.UserName, rabbitMqSettings.Password, "None", "RabbitMq")}
                }),
                new RabbitMqTransportFactory());
#else
            var messagingEngine = new MessagingEngine(_log,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"RabbitMq", new TransportInfo(rabbitMqSettings.Endpoint.ToString(), rabbitMqSettings.UserName, rabbitMqSettings.Password, "None", "RabbitMq")}
                }),
                new RabbitMqTransportFactory());
#endif

            var defaultRetryDelay = _settings.TransactionHandlerJob.RetryDelayInMilliseconds;

            builder.RegisterType<CashInOutMessageProcessor>();
            builder.RegisterType<CashInOutSaga>();
            builder.RegisterType<ForwardWithdawalSaga>();

            builder.RegisterType<ForwardWithdrawalCommandHandler>();
            builder.RegisterType<BitcoinCommandHandler>()
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(defaultRetryDelay)))
                .WithParameter(TypedParameter.From(AzureQueueExt.Create(_dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString), "intransactions")));
            builder.RegisterType<ChronoBankCommandHandler>();
            builder.RegisterType<EthereumCommandHandler>()
                .WithParameter(TypedParameter.From(_settings.Ethereum))
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(defaultRetryDelay)));
            builder.RegisterType<SolarCoinCommandHandler>();
            builder.RegisterType<OffchainCommandHandler>();
            builder.RegisterType<OperationsCommandHandler>();
            builder.RegisterType<TransactionsCommandHandler>();

            builder.RegisterType<OperationHistoryProjection>();
            builder.RegisterType<NotificationsProjection>();

            builder.Register(ctx =>
            {
                var defaultPipeline = "commands";
                var defaultRoute = "self";
                var historyProjection = ctx.Resolve<OperationHistoryProjection>();
                var notificationsProjection = ctx.Resolve<NotificationsProjection>();
                return new CqrsEngine(_log,
                    ctx.Resolve<IDependencyResolver>(),
                    messagingEngine,
                    new DefaultEndpointProvider(),
                    true,
                    Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver("RabbitMq", "protobuf", environment: _settings.TransactionHandlerJob.Environment)),

                Register.BoundedContext("tx-handler"),

                Register.BoundedContext("forward-withdrawal")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SetLinkedCashInOperationCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(ForwardWithdawalLinkedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<ForwardWithdrawalCommandHandler>(),

                Register.BoundedContext("bitcoin")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SendBitcoinCommand), typeof(BitcoinCashOutCommand), typeof(SegwitTransferCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<BitcoinCommandHandler>(),

                Register.BoundedContext("chronobank")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ChronoBankCashOutCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<ChronoBankCommandHandler>(),

                Register.BoundedContext("ethereum")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ProcessEthereumCashoutCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<EthereumCommandHandler>(),

                Register.BoundedContext("offchain")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateOffchainCashoutRequestCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<OffchainCommandHandler>(),

                Register.BoundedContext("solarcoin")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SolarCashOutCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(SolarCashOutCompletedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<SolarCoinCommandHandler>(),

                Register.BoundedContext("operations")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(RegisterCashInOutOperationCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<OperationsCommandHandler>(),

                Register.BoundedContext("transactions")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SaveCashoutTransactionStateCommand), typeof(SaveDestroyTransactionStateCommand), typeof(SaveIssueTransactionStateCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(IssueTransactionStateSavedEvent), typeof(DestroyTransactionStateSavedEvent), typeof(CashoutTransactionStateSavedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<TransactionsCommandHandler>(),

                Register.BoundedContext("history")
                    .ListeningEvents(typeof(IssueTransactionStateSavedEvent), typeof(DestroyTransactionStateSavedEvent),
                            typeof(CashoutTransactionStateSavedEvent))
                        .From("transactions").On(defaultRoute)
                    .WithProjection(historyProjection, "transactions")
                    .ListeningEvents(typeof(ForwardWithdawalLinkedEvent))
                        .From("forward-withdrawal").On(defaultRoute)
                    .WithProjection(historyProjection, "forward-withdrawal"),

                Register.BoundedContext("notifications")
                    .ListeningEvents(typeof(SolarCashOutCompletedEvent))
                        .From("solarcoin").On(defaultRoute)
                    .WithProjection(notificationsProjection, "solarcoin"),

                Register.Saga<CashInOutSaga>("cash-out-saga")
                    .ListeningEvents(typeof(DestroyTransactionStateSavedEvent), typeof(CashoutTransactionStateSavedEvent))
                        .From("transactions").On(defaultRoute)
                    .PublishingCommands(typeof(SendBitcoinCommand), typeof(BitcoinCashOutCommand))
                        .To("bitcoin").With(defaultPipeline)
                    .PublishingCommands(typeof(ChronoBankCashOutCommand))
                        .To("chronobank").With(defaultPipeline)
                    .PublishingCommands(typeof(ProcessEthereumCashoutCommand))
                        .To("ethereum").With(defaultPipeline)
                    .PublishingCommands(typeof(SolarCashOutCommand))
                        .To("solarcoin").With(defaultPipeline),

                Register.Saga<ForwardWithdawalSaga>("forward-withdrawal-saga")
                    .ListeningEvents(typeof(CashoutTransactionStateSavedEvent))
                        .From("transactions").On(defaultRoute)
                    .PublishingCommands(typeof(SetLinkedCashInOperationCommand))
                        .To("forward-withdrawal").With(defaultPipeline),

                Register.DefaultRouting
                    .PublishingCommands(typeof(CreateOffchainCashoutRequestCommand))
                        .To("offchain").With(defaultPipeline)
                    .PublishingCommands(typeof(SegwitTransferCommand))
                        .To("bitcoin").With(defaultPipeline)
                    .PublishingCommands(typeof(RegisterCashInOutOperationCommand))
                        .To("operations").With(defaultPipeline)
                    .PublishingCommands(typeof(SaveIssueTransactionStateCommand), typeof(SaveDestroyTransactionStateCommand), typeof(SaveCashoutTransactionStateCommand))
                        .To("transactions").With(defaultPipeline)
                );
            })
            .As<ICqrsEngine>().SingleInstance();
        }

        private void InitSerializer()
        {
            SerializerBuilder.Build<Service.Assets.Client.Models.Asset>();
            SerializerBuilder.Build<Core.Domain.BitCoin.CashOutCommand>();
            SerializerBuilder.Build<Core.Domain.BitCoin.CashOutContextData>();
            SerializerBuilder.Build<Core.Domain.BitCoin.DestroyCommand>();
            SerializerBuilder.Build<Core.Domain.BitCoin.UncolorContextData>();
            SerializerBuilder.Build<Core.Domain.BitCoin.IssueCommand>();
            SerializerBuilder.Build<Core.Domain.BitCoin.IssueContextData>();
        }
    }
}
