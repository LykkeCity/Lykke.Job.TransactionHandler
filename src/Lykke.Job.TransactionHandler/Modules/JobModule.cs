using Autofac;
using AutoMapper;
using AzureStorage.Blob;
using AzureStorage.Queue;
using AzureStorage.Tables;
using AzureStorage.Tables.Templates.Index;
using Lykke.Bitcoin.Api.Client;
using Lykke.Job.TransactionHandler.AzureRepositories.BitCoin;
using Lykke.Job.TransactionHandler.AzureRepositories.Blockchain;
using Lykke.Job.TransactionHandler.AzureRepositories.Clients;
using Lykke.Job.TransactionHandler.AzureRepositories.Ethereum;
using Lykke.Job.TransactionHandler.AzureRepositories.Exchange;
using Lykke.Job.TransactionHandler.AzureRepositories.Messages.Email;
using Lykke.Job.TransactionHandler.AzureRepositories.Offchain;
using Lykke.Job.TransactionHandler.AzureRepositories.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Core.Domain.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Fee;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.Sender;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Queues;
using Lykke.Job.TransactionHandler.Services.BitCoin;
using Lykke.Job.TransactionHandler.Services.Ethereum;
using Lykke.Job.TransactionHandler.Services.Fee;
using Lykke.Job.TransactionHandler.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Services.Offchain;
using Lykke.Service.Assets.Client;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.EthereumCore.Client;
using Lykke.Service.ExchangeOperations.Client;
using Lykke.Service.Operations.Client;
using Lykke.Service.OperationsRepository.Client;
using Lykke.Service.PersonalData.Client;
using Lykke.Service.PersonalData.Contract;
using Lykke.SettingsReader;
using System;
using System.Threading.Tasks;
using Autofac.Extensions.DependencyInjection;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Settings;
using Lykke.JobTriggers.Extenstions;
using Lykke.JobTriggers.Triggers;
using Lykke.Sdk;

namespace Lykke.Job.TransactionHandler.Modules
{
    [UsedImplicitly]
    public class JobModule : Module
    {
        private readonly AppSettings _settings;
        private readonly TransactionHandlerSettings _jobSettings;
        private readonly IReloadingManager<DbSettings> _dbSettingsManager;

        public JobModule(IReloadingManager<AppSettings> appSettings)
        {
            _settings = appSettings.CurrentValue;
            _jobSettings = _settings.TransactionHandlerJob;
            _dbSettingsManager = appSettings.Nested(x => x.TransactionHandlerJob.Db);
        }

        protected override void Load(ContainerBuilder builder)
        {
            Mapper.Initialize(cfg =>
            {
                cfg.CreateMap<IBcnCredentialsRecord, BcnCredentialsRecordEntity>().IgnoreTableEntityFields();
                cfg.CreateMap<IEthereumTransactionRequest, EthereumTransactionReqEntity>().IgnoreTableEntityFields()
                    .ForMember(x => x.SignedTransferVal, config => config.Ignore())
                    .ForMember(x => x.OperationIdsVal, config => config.Ignore());
            });

            Mapper.Configuration.AssertConfigurationIsValid();

            builder.Register(ctx =>
                {
                    var scope = ctx.Resolve<ILifetimeScope>();
                    var host = new TriggerHost(new AutofacServiceProvider(scope));
                    return host;
                }).As<TriggerHost>()
                .SingleInstance();

            builder.RegisterType<StartupManager>()
                .As<IStartupManager>()
                .SingleInstance();

            builder.RegisterType<ShutdownManager>()
                .As<IShutdownManager>()
                .SingleInstance();

            if (string.IsNullOrWhiteSpace(_jobSettings.Db.BitCoinQueueConnectionString))
            {
                builder.AddTriggers();
            }
            else
            {
                builder.AddTriggers(pool =>
                {
                    pool.AddDefaultConnection(_dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString));
                });
            }

            BindRabbitMq(builder);
            BindRepositories(builder);
            BindServices(builder);
            BindClients(builder);
        }

        private void BindClients(ContainerBuilder builder)
        {
            builder.RegisterAssetsClient(
                AssetServiceSettings.Create(new Uri(_settings.Assets.ServiceUrl),
                    _jobSettings.AssetsCache.ExpirationPeriod));

            builder.RegisterType<PersonalDataService>()
                .As<IPersonalDataService>()
                .WithParameter(TypedParameter.From(_settings.PersonalDataServiceSettings));

            builder.RegisterLykkeServiceClient(_settings.ClientAccountClient.ServiceUrl);
            builder.RegisterOperationsClient(_settings.TransactionHandlerJob.Services.OperationsUrl);
            builder.RegisterExchangeOperationsClient(_jobSettings.ExchangeOperationsServiceUrl);

            builder.Register<IEthereumCoreAPI>(x =>
            {
                var api = new EthereumCoreAPI(new Uri(_settings.Ethereum.EthereumCoreUrl));
                api.SetRetryPolicy(null);
                return api;
            }).SingleInstance();

            builder.RegisterOperationsRepositoryClients(_settings.OperationsRepositoryServiceClient);
            builder.RegisterBitcoinApiClient(_settings.BitCoinCore.BitcoinCoreApiUrl);

            builder.RegisterMeClient(_settings.MatchingEngineClient.IpEndpoint.GetClientIpEndPoint());
        }

        private void BindServices(ContainerBuilder builder)
        {
            builder.RegisterType<OffchainRequestService>().As<IOffchainRequestService>().SingleInstance();

            builder.RegisterType<SrvEthereumHelper>().As<ISrvEthereumHelper>().SingleInstance();

            builder.RegisterType<EmailSender>().As<IEmailSender>().SingleInstance();

            builder.RegisterType<SrvEmailsFacade>().As<ISrvEmailsFacade>().SingleInstance();

            builder.RegisterType<TransactionService>().As<ITransactionService>().SingleInstance();

            builder.RegisterType<FeeCalculationService>().As<IFeeCalculationService>().SingleInstance();
        }

        private void BindRepositories(ContainerBuilder builder)
        {
            builder.Register<ITransactionsRepository>(ctx =>
                new TransactionsRepository(
                    AzureTableStorage<BitCoinTransactionEntity>.Create(
                        _dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString),
                        "BitCoinTransactions", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.Register<IWalletCredentialsRepository>(ctx =>
                new WalletCredentialsRepository(
                    AzureTableStorage<WalletCredentialsEntity>.Create(
                        _dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString),
                        "WalletCredentials", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.Register<IBcnClientCredentialsRepository>(ctx =>
                new BcnClientCredentialsRepository(
                    AzureTableStorage<BcnCredentialsRecordEntity>.Create(
                        _dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString),
                        "BcnClientCredentials", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.Register<IClientCacheRepository>(ctx =>
                new ClientCacheRepository(
                    AzureTableStorage<ClientCacheEntity>.Create(
                        _dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString),
                        "ClientCache", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.Register<IEthereumTransactionRequestRepository>(ctx =>
                new EthereumTransactionRequestRepository(
                    AzureTableStorage<EthereumTransactionReqEntity>.Create(
                        _dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString),
                        "EthereumTxRequest", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.Register<IBitcoinCashinRepository>(ctx =>
                new BitcoinCashinRepository(
                    AzureTableStorage<BitcoinCashinEntity>.Create(
                        _dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString),
                        "BitcoinCashin", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.Register<IMarketOrdersRepository>(ctx =>
                new MarketOrdersRepository(AzureTableStorage<MarketOrderEntity>.Create(
                    _dbSettingsManager.ConnectionString(x => x.HMarketOrdersConnString),
                    "MarketOrders", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.Register<ILimitOrdersRepository>(ctx =>
                new LimitOrdersRepository(AzureTableStorage<LimitOrderEntity>.Create(
                    _dbSettingsManager.ConnectionString(x => x.HMarketOrdersConnString),
                    "LimitOrders", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.RegisterInstance<IEmailCommandProducer>(
                new EmailCommandProducer(AzureQueueExt.Create(
                    _dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString),
                    "emailsqueue")));

            builder.Register<IOffchainRequestRepository>(ctx =>
                new OffchainRequestRepository(
                    AzureTableStorage<OffchainRequestEntity>.Create(
                        _dbSettingsManager.ConnectionString(x => x.OffchainConnString),
                        "OffchainRequests", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.Register<IOffchainTransferRepository>(ctx =>
                new OffchainTransferRepository(
                    AzureTableStorage<OffchainTransferEntity>.Create(
                        _dbSettingsManager.ConnectionString(x => x.OffchainConnString),
                        "OffchainTransfers", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.Register<IPaymentTransactionsRepository>(ctx =>
                new PaymentTransactionsRepository(
                    AzureTableStorage<PaymentTransactionEntity>.Create(
                        _dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString),
                        "PaymentTransactions", ctx.Resolve<ILogFactory>()),
                    AzureTableStorage<AzureMultiIndex>.Create(
                        _dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString),
                        "PaymentTransactions", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.RegisterInstance(
                    new BitcoinTransactionContextBlobStorage(AzureBlobStorage.Create(
                        _dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString))))
                .As<IBitcoinTransactionContextBlobStorage>();

            builder.Register<IEthererumPendingActionsRepository>(ctx =>
              new EthererumPendingActionsRepository(
                  AzureTableStorage<EthererumPendingActionEntity>.Create(
                      _dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString),
                      "EthererumPendingActions", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.Register<IClientCommentsRepository>(ctx =>
                new ClientCommentsRepository(AzureTableStorage<ClientCommentEntity>.Create(
                    _dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString),
                    "ClientComments", ctx.Resolve<ILogFactory>())))
                .SingleInstance();

            builder.Register(ctx =>
                    EthereumCashinAggregateRepository.Create(
                        _dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString),
                        ctx.Resolve<ILogFactory>()))
                .As<IEthereumCashinAggregateRepository>()
                .SingleInstance();
        }

        private void BindRabbitMq(ContainerBuilder builder)
        {
            builder.RegisterInstance(_jobSettings.MongoDeduplicator);
            builder.RegisterInstance(_settings.RabbitMq);
            builder.RegisterInstance(_settings.EthRabbitMq);

            builder.RegisterType<CashInOutQueue>()
                .As<IStartable>()
                .AutoActivate()
                .SingleInstance();

            builder.RegisterType<TransferQueue>()
                .As<IStartable>()
                .AutoActivate()
                .SingleInstance();

            builder.RegisterType<TradeQueue>()
                .As<IStartable>()
                .AutoActivate()
                .SingleInstance();

            builder.RegisterType<EthereumEventsQueue>()
                .As<IStartable>()
                .AutoActivate()
                .SingleInstance();
        }
    }
}
