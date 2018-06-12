using System;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AutoMapper;
using AzureStorage.Blob;
using AzureStorage.Queue;
using AzureStorage.Tables;
using AzureStorage.Tables.Templates.Index;
using Common.Log;
using Lykke.Bitcoin.Api.Client;
using Lykke.Job.TransactionHandler.AzureRepositories.BitCoin;
using Lykke.Job.TransactionHandler.AzureRepositories.Blockchain;
using Lykke.Job.TransactionHandler.AzureRepositories.CashOperations;
using Lykke.Job.TransactionHandler.AzureRepositories.ChronoBank;
using Lykke.Job.TransactionHandler.AzureRepositories.Clients;
using Lykke.Job.TransactionHandler.AzureRepositories.Common;
using Lykke.Job.TransactionHandler.AzureRepositories.Ethereum;
using Lykke.Job.TransactionHandler.AzureRepositories.Exchange;
using Lykke.Job.TransactionHandler.AzureRepositories.MarginTrading;
using Lykke.Job.TransactionHandler.AzureRepositories.Messages.Email;
using Lykke.Job.TransactionHandler.AzureRepositories.Offchain;
using Lykke.Job.TransactionHandler.AzureRepositories.PaymentSystems;
using Lykke.Job.TransactionHandler.AzureRepositories.Quanta;
using Lykke.Job.TransactionHandler.AzureRepositories.SolarCoin;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Core.Domain.ChronoBank;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Core.Domain.MarginTrading;
using Lykke.Job.TransactionHandler.Core.Domain.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Domain.Quanta;
using Lykke.Job.TransactionHandler.Core.Domain.SolarCoin;
using Lykke.Job.TransactionHandler.Core.Services;
using Lykke.Job.TransactionHandler.Core.Services.AppNotifications;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.MarginTrading;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.Sender;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.Quanta;
using Lykke.Job.TransactionHandler.Queues;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Services.BitCoin;
using Lykke.Job.TransactionHandler.Services.Ethereum;
using Lykke.Job.TransactionHandler.AzureRepositories.Fee;
using Lykke.Job.TransactionHandler.Core.Domain.Fee;
using Lykke.Job.TransactionHandler.Services.Http;
using Lykke.Job.TransactionHandler.Services.MarginTrading;
using Lykke.Job.TransactionHandler.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Services.Notifications;
using Lykke.Job.TransactionHandler.Services.Offchain;
using Lykke.Job.TransactionHandler.Services.Quanta;
using Lykke.Service.Assets.Client;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.ExchangeOperations.Client;
using Lykke.Service.Operations.Client;
using Lykke.Service.PersonalData.Client;
using Lykke.Service.PersonalData.Contract;
using Lykke.SettingsReader;
using Microsoft.Extensions.DependencyInjection;
using Lykke.Service.EthereumCore.Client;
using Lykke.Service.OperationsRepository.Client;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Common;
using Lykke.Job.TransactionHandler.Core.Services.Fee;
using Lykke.Job.TransactionHandler.Services.Fee;

namespace Lykke.Job.TransactionHandler.Modules
{
    public class JobModule : Module
    {
        private readonly AppSettings _settings;
        private readonly AppSettings.TransactionHandlerSettings _jobSettings;
        private readonly IReloadingManager<AppSettings.DbSettings> _dbSettingsManager;
        private readonly ILog _log;
        // NOTE: you can remove it if you don't need to use IServiceCollection extensions to register service specific dependencies
        private readonly IServiceCollection _services;

        public JobModule(AppSettings settings, IReloadingManager<AppSettings.DbSettings> dbSettingsManagerManager, ILog log)
        {
            _settings = settings;
            _jobSettings = _settings.TransactionHandlerJob;
            _dbSettingsManager = dbSettingsManagerManager;
            _log = log;

            _services = new ServiceCollection();
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(_settings.TransactionHandlerJob)
                .SingleInstance();

            builder.RegisterInstance(_log)
                .As<ILog>()
                .SingleInstance();

            builder.RegisterType<HealthService>()
                .As<IHealthService>()
                .SingleInstance()
                .WithParameter(TypedParameter.From(TimeSpan.FromSeconds(30)));

            // NOTE: You can implement your own poison queue notifier. See https://github.com/LykkeCity/JobTriggers/blob/master/readme.md
            // builder.Register<PoisionQueueNotifierImplementation>().As<IPoisionQueueNotifier>();

            builder.RegisterInstance(_settings.Ethereum).SingleInstance();

            _services.RegisterAssetsClient(AssetServiceSettings.Create(new Uri(_settings.Assets.ServiceUrl), _jobSettings.AssetsCache.ExpirationPeriod));

            Mapper.Initialize(cfg =>
            {
                cfg.CreateMap<IBcnCredentialsRecord, BcnCredentialsRecordEntity>().IgnoreTableEntityFields();
                cfg.CreateMap<IEthereumTransactionRequest, EthereumTransactionReqEntity>().IgnoreTableEntityFields()
                    .ForMember(x => x.SignedTransferVal, config => config.Ignore())
                    .ForMember(x => x.OperationIdsVal, config => config.Ignore());
            });

            Mapper.Configuration.AssertConfigurationIsValid();

            BindRabbitMq(builder);
            BindRepositories(builder);
            BindServices(builder);
            BindClients(builder);

            builder.Populate(_services);
        }

        private void BindClients(ContainerBuilder builder)
        {
            builder.RegisterType<PersonalDataService>()
                .As<IPersonalDataService>()
                .WithParameter(TypedParameter.From(_settings.PersonalDataServiceSettings));

            builder.RegisterLykkeServiceClient(_settings.ClientAccountClient.ServiceUrl);
            builder.RegisterOperationsClient(_settings.TransactionHandlerJob.Services.OperationsUrl);
        }

        private void BindServices(ContainerBuilder builder)
        {
            builder.RegisterType<HttpRequestClient>().SingleInstance();

            builder.RegisterType<OffchainRequestService>().As<IOffchainRequestService>();
            builder.RegisterType<SrvSlackNotifications>()
                .SingleInstance()
                .WithParameter(TypedParameter.From(_settings.SlackIntegration));

            var exchangeOperationsService = new ExchangeOperationsServiceClient(_jobSettings.ExchangeOperationsServiceUrl);
            builder.RegisterInstance(exchangeOperationsService).As<IExchangeOperationsServiceClient>().SingleInstance();

            builder.Register<IAppNotifications>(x => new SrvAppNotifications(
                _settings.AppNotifications.HubConnString,
                _settings.AppNotifications.HubName));

            builder.RegisterType<QuantaService>().As<IQuantaService>().SingleInstance();

            builder.Register<IEthereumCoreAPI>(x =>
            {
                var api = new EthereumCoreAPI(new Uri(_settings.Ethereum.EthereumCoreUrl));
                api.SetRetryPolicy(null);
                return api;
            }).SingleInstance();

            builder.RegisterType<SrvEthereumHelper>().As<ISrvEthereumHelper>().SingleInstance();

            builder.RegisterType<MarginDataServiceResolver>()
                .As<IMarginDataServiceResolver>()
                .SingleInstance()
                .WithParameter(TypedParameter.From(_settings.MarginTrading));

            builder.RegisterType<EmailSender>().As<IEmailSender>().SingleInstance();
            builder.RegisterType<SrvEmailsFacade>().As<ISrvEmailsFacade>().SingleInstance();

            builder.RegisterType<TransactionService>().As<ITransactionService>().SingleInstance();
            builder.RegisterType<NotificationsService>().As<INotificationsService>().SingleInstance();

            builder.RegisterOperationsRepositoryClients(_settings.OperationsRepositoryServiceClient, _log);

            builder.RegisterBitcoinApiClient(_settings.BitCoinCore.BitcoinCoreApiUrl);

            builder.RegisterType<PersistentDeduplicator>().As<IDeduplicator>().SingleInstance();

            builder.RegisterType<FeeLogService>().As<IFeeLogService>().SingleInstance();

            builder.RegisterType<FeeCalculationService>().As<IFeeCalculationService>().SingleInstance();
        }

        private void BindRepositories(ContainerBuilder builder)
        {
            builder.RegisterInstance<ITransactionsRepository>(
                new TransactionsRepository(
                    AzureTableStorage<BitCoinTransactionEntity>.Create(_dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString), "BitCoinTransactions", _log)));

            builder.RegisterInstance<IWalletCredentialsRepository>(
                new WalletCredentialsRepository(
                    AzureTableStorage<WalletCredentialsEntity>.Create(_dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "WalletCredentials", _log)));

            builder.RegisterInstance<IBcnClientCredentialsRepository>(
                new BcnClientCredentialsRepository(
                    AzureTableStorage<BcnCredentialsRecordEntity>.Create(_dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "BcnClientCredentials", _log)));

            builder.RegisterInstance<IForwardWithdrawalRepository>(
                new ForwardWithdrawalRepository(
                    AzureTableStorage<ForwardWithdrawalEntity>.Create(_dbSettingsManager.ConnectionString(x => x.BalancesInfoConnString), "ForwardWithdrawal", _log)));

            builder.RegisterInstance<IChronoBankCommandProducer>(
                new SrvChronoBankCommandProducer(AzureQueueExt.Create(_dbSettingsManager.ConnectionString(x => x.ChronoBankSrvConnString), "chronobank-out")));

            builder.RegisterInstance<IClientSettingsRepository>(
                new ClientSettingsRepository(
                    AzureTableStorage<ClientSettingsEntity>.Create(_dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "TraderSettings", _log)));

            builder.RegisterInstance<IClientCacheRepository>(
                new ClientCacheRepository(
                    AzureTableStorage<ClientCacheEntity>.Create(_dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "ClientCache", _log)));

            builder.RegisterInstance<IEthClientEventLogs>(
                new EthClientEventLogs(
                    AzureTableStorage<EthClientEventRecord>.Create(_dbSettingsManager.ConnectionString(x => x.LwEthLogsConnString), "EthClientEventLogs", _log)));

            builder.RegisterInstance<IEthereumTransactionRequestRepository>(
                new EthereumTransactionRequestRepository(
                    AzureTableStorage<EthereumTransactionReqEntity>.Create(_dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString), "EthereumTxRequest", _log)));

            builder.RegisterInstance<IBitcoinCashinRepository>(
                new BitcoinCashinRepository(
                    AzureTableStorage<BitcoinCashinEntity>.Create(
                        _dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString), "BitcoinCashin", _log)));

            builder.RegisterInstance<IMarketOrdersRepository>(
                new MarketOrdersRepository(AzureTableStorage<MarketOrderEntity>.Create(_dbSettingsManager.ConnectionString(x => x.HMarketOrdersConnString), "MarketOrders", _log)));

            builder.RegisterInstance<ILimitOrdersRepository>(
                new LimitOrdersRepository(AzureTableStorage<LimitOrderEntity>.Create(_dbSettingsManager.ConnectionString(x => x.HMarketOrdersConnString), "LimitOrders", _log)));

            builder.RegisterInstance<IMarginTradingPaymentLogRepository>(
                new MarginTradingPaymentLogRepository(
                    AzureTableStorage<MarginTradingPaymentLogEntity>.Create(_dbSettingsManager.ConnectionString(x => x.LogsConnString), "MarginTradingPaymentsLog", _log)));

            builder.RegisterInstance<IEmailCommandProducer>(
                new EmailCommandProducer(AzureQueueExt.Create(_dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "emailsqueue")));

            builder.RegisterInstance<IOffchainRequestRepository>(
                new OffchainRequestRepository(
                    AzureTableStorage<OffchainRequestEntity>.Create(_dbSettingsManager.ConnectionString(x => x.OffchainConnString), "OffchainRequests", _log)));

            builder.RegisterInstance<IOffchainTransferRepository>(
                new OffchainTransferRepository(
                    AzureTableStorage<OffchainTransferEntity>.Create(_dbSettingsManager.ConnectionString(x => x.OffchainConnString), "OffchainTransfers", _log)));

            builder.RegisterInstance<IPaymentTransactionsRepository>(
                new PaymentTransactionsRepository(
                    AzureTableStorage<PaymentTransactionEntity>.Create(_dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "PaymentTransactions", _log),
                    AzureTableStorage<AzureMultiIndex>.Create(_dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "PaymentTransactions", _log)));

            builder.RegisterInstance<IQuantaCommandProducer>(
                new SrvQuantaCommandProducer(AzureQueueExt.Create(_dbSettingsManager.ConnectionString(x => x.QuantaSrvConnString), "quanta-out")));

            builder.RegisterInstance<ISrvSolarCoinCommandProducer>(
                new SrvSolarCoinCommandProducer(AzureQueueExt.Create(_dbSettingsManager.ConnectionString(x => x.SolarCoinConnString), "solar-out")));

            builder.RegisterInstance(new BitcoinTransactionContextBlobStorage(AzureBlobStorage.Create(_dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString))))
                .As<IBitcoinTransactionContextBlobStorage>();

            builder.RegisterInstance<IEthererumPendingActionsRepository>(
              new EthererumPendingActionsRepository(
                  AzureTableStorage<EthererumPendingActionEntity>.Create(
                      _dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString), "EthererumPendingActions", _log)));

            builder.RegisterType<FeeLogRepository>()
                .WithParameter(TypedParameter.From(AzureTableStorage<FeeLogEntryEntity>.Create(
                    _dbSettingsManager.ConnectionString(x => x.FeeLogsConnString), "OperationsFeeLog", _log)))
                .As<IFeeLogRepository>()
                .SingleInstance();

            builder.RegisterInstance<IBlobRepository>(
                new BlobRepository(
                    AzureTableStorage<BlobEntity>.Create(_dbSettingsManager.ConnectionString(x => x.IncomingMessagesConnString), "IncomingMessages", _log)));

            builder.RegisterInstance<IClientCommentsRepository>(
                new ClientCommentsRepository(AzureTableStorage<ClientCommentEntity>.Create(_dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "ClientComments", _log)));

            builder.RegisterInstance<IEthereumCashinAggregateRepository>(
                    EthereumCashinAggregateRepository.Create(
                        _dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString), _log));
        }

        private void BindRabbitMq(ContainerBuilder builder)
        {
            builder.RegisterInstance(_settings.EthRabbitMq);
            builder.RegisterInstance(_settings.RabbitMq);
            builder.RegisterType<CashInOutQueue>().SingleInstance();
            builder.RegisterType<TransferQueue>().SingleInstance();
            builder.RegisterType<LimitTradeQueue>().SingleInstance();
            builder.RegisterType<TradeQueue>().SingleInstance();
            builder.RegisterType<EthereumEventsQueue>().SingleInstance();
        }
    }
}