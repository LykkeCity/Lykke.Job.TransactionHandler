using System;
using System.Linq;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AutoMapper;
using AzureStorage.Blob;
using AzureStorage.Queue;
using AzureStorage.Tables;
using AzureStorage.Tables.Templates.Index;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.AzureRepositories.Assets;
using Lykke.Job.TransactionHandler.AzureRepositories.BitCoin;
using Lykke.Job.TransactionHandler.AzureRepositories.Blockchain;
using Lykke.Job.TransactionHandler.AzureRepositories.CashOperations;
using Lykke.Job.TransactionHandler.AzureRepositories.ChronoBank;
using Lykke.Job.TransactionHandler.AzureRepositories.Clients;
using Lykke.Job.TransactionHandler.AzureRepositories.Ethereum;
using Lykke.Job.TransactionHandler.AzureRepositories.Exchange;
using Lykke.Job.TransactionHandler.AzureRepositories.MarginTrading;
using Lykke.Job.TransactionHandler.AzureRepositories.Messages.Email;
using Lykke.Job.TransactionHandler.AzureRepositories.Offchain;
using Lykke.Job.TransactionHandler.AzureRepositories.PaymentSystems;
using Lykke.Job.TransactionHandler.AzureRepositories.Quanta;
using Lykke.Job.TransactionHandler.AzureRepositories.SolarCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Assets;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Core.Domain.ChronoBank;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
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
using Lykke.Job.TransactionHandler.Core.Services.BitCoin.BitCoinApi;
using Lykke.Job.TransactionHandler.Core.Services.ChronoBank;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.MarginTrading;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.Sender;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.Quanta;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;
using Lykke.Job.TransactionHandler.Queues;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Services.BitCoin;
using Lykke.Job.TransactionHandler.Services.BitCoin.BitCoinApiClient;
using Lykke.Job.TransactionHandler.Services.ChronoBank;
using Lykke.Job.TransactionHandler.Services.Ethereum;
using Lykke.Job.TransactionHandler.Services.Generated.EthereumCoreApi;
using Lykke.Job.TransactionHandler.Services.Http;
using Lykke.Job.TransactionHandler.Services.MarginTrading;
using Lykke.Job.TransactionHandler.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Services.Notifications;
using Lykke.Job.TransactionHandler.Services.Offchain;
using Lykke.Job.TransactionHandler.Services.Quanta;
using Lykke.Job.TransactionHandler.Services.SolarCoin;
using Lykke.MatchingEngine.Connector.Services;
using Lykke.Service.Assets.Client.Custom;
using Lykke.Service.ExchangeOperations.Client;
using Lykke.Service.ExchangeOperations.Contracts;
using Lykke.Service.OperationsHistory.HistoryWriter.Abstractions;
using Lykke.Service.OperationsHistory.HistoryWriter.Implementation;
using Lykke.Service.PersonalData.Client;
using Lykke.Service.PersonalData.Contract;
using Microsoft.Extensions.DependencyInjection;

namespace Lykke.Job.TransactionHandler.Modules
{
    public class JobModule : Module
    {
        private readonly AppSettings _settings;
        private AppSettings.TransactionHandlerSettings _jobSettings;
        private readonly AppSettings.DbSettings _dbSettings;
        private readonly ILog _log;
        // NOTE: you can remove it if you don't need to use IServiceCollection extensions to register service specific dependencies
        private readonly IServiceCollection _services;



        public JobModule(AppSettings settings, ILog log)
        {
            _settings = settings;
            _jobSettings = _settings.TransactionHandlerJob;
            _dbSettings = settings.TransactionHandlerJob.Db;
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

            _services.UseAssetsClient(new AssetServiceSettings
            {
                BaseUri = new Uri(_settings.Assets.ServiceUrl),
                AssetPairsCacheExpirationPeriod = _jobSettings.AssetsCache.ExpirationPeriod,
                AssetsCacheExpirationPeriod = _jobSettings.AssetsCache.ExpirationPeriod
            });

            Mapper.Initialize(cfg => cfg.CreateMap<IBcnCredentialsRecord, BcnCredentialsRecordEntity>().IgnoreTableEntityFields());

            Mapper.Initialize(cfg => cfg.CreateMap<IEthereumTransactionRequest, EthereumTransactionReqEntity>().IgnoreTableEntityFields()
                .ForMember(x => x.SignedTransferVal, config => config.Ignore())
                .ForMember(x => x.OperationIdsVal, config => config.Ignore()));

            Mapper.Configuration.AssertConfigurationIsValid();

            BindRabbitMq(builder);
            BindMatchingEngineChannel(builder);
            BindRepositories(builder);
            BindServices(builder);
            BindCachedDicts(builder);

            builder.Populate(_services);
        }

        public static void BindCachedDicts(ContainerBuilder builder)
        {
            builder.Register(x =>
            {
                var ctx = x.Resolve<IComponentContext>();
                return new CachedDataDictionary<string, IAssetSetting>(
                    async () => (await ctx.Resolve<IAssetSettingRepository>().GetAssetSettings()).ToDictionary(itm => itm.Asset));
            }).SingleInstance();
        }

        private void BindMatchingEngineChannel(ContainerBuilder container)
        {
            var socketLog = new SocketLogDynamic(i => { },
                str => Console.WriteLine(DateTime.UtcNow.ToIsoDateTime() + ": " + str));

            container.BindMeClient(_settings.MatchingEngineClient.IpEndpoint.GetClientIpEndPoint(), socketLog);
        }

        private void BindServices(ContainerBuilder builder)
        {
            builder.RegisterType<HttpRequestClient>().SingleInstance();
            builder.RegisterType<BitcoinApiClient>()
                .As<IBitcoinApiClient>()
                .SingleInstance()
                .WithParameter(TypedParameter.From(_settings.BitCoinCore));
            builder.RegisterType<OffchainRequestService>().As<IOffchainRequestService>();
            builder.RegisterType<SrvSlackNotifications>()
                .SingleInstance()
                .WithParameter(TypedParameter.From(_settings.SlackIntegration));

            var exchangeOperationsService = new ExchangeOperationsServiceClient(_jobSettings.ExchangeOperationsServiceUrl);
            builder.RegisterInstance(exchangeOperationsService).As<IExchangeOperationsService>().SingleInstance();

            builder.Register<IAppNotifications>(x => new SrvAppNotifications(
                _settings.AppNotifications.HubConnString,
                _settings.AppNotifications.HubName));

            builder.RegisterType<ChronoBankService>().As<IChronoBankService>().SingleInstance();
            builder.RegisterType<SrvSolarCoinHelper>().As<ISrvSolarCoinHelper>().SingleInstance();
            builder.RegisterType<QuantaService>().As<IQuantaService>().SingleInstance();

            builder.Register<IEthereumApi>(x =>
            {
                var api = new EthereumApi(new Uri(_settings.Ethereum.EthereumCoreUrl));
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

            builder.RegisterType<BitcoinTransactionService>().As<IBitcoinTransactionService>().SingleInstance();

            var historyWriter = new HistoryWriter(_dbSettings.HistoryLogsConnString, _log);
            builder.RegisterInstance(historyWriter).As<IHistoryWriter>();

            builder.RegisterType<PersonalDataService>()
                .As<IPersonalDataService>()
                .WithParameter(TypedParameter.From(_settings.PersonalDataServiceSettings));
        }

        private void BindRepositories(ContainerBuilder builder)
        {
            builder.RegisterInstance<IAssetSettingRepository>(
                new AssetSettingRepository(
                    new AzureTableStorage<AssetSettingEntity>(_dbSettings.DictsConnString, "AssetSettings", _log)));

            builder.RegisterInstance<IBitcoinCommandSender>(
                new BitcoinCommandSender(
                    new AzureQueueExt(_dbSettings.BitCoinQueueConnectionString, "intransactions")));

            builder.RegisterInstance<IBitCoinTransactionsRepository>(
                new BitCoinTransactionsRepository(
                    new AzureTableStorage<BitCoinTransactionEntity>(_dbSettings.BitCoinQueueConnectionString, "BitCoinTransactions", _log)));

            builder.RegisterInstance<IWalletCredentialsRepository>(
                new WalletCredentialsRepository(
                    new AzureTableStorage<WalletCredentialsEntity>(_dbSettings.ClientPersonalInfoConnString, "WalletCredentials", _log)));

            builder.RegisterInstance<IBcnClientCredentialsRepository>(
                new BcnClientCredentialsRepository(
                    new AzureTableStorage<BcnCredentialsRecordEntity>(_dbSettings.ClientPersonalInfoConnString, "BcnClientCredentials", _log)));

            builder.RegisterInstance<ICashOperationsRepository>(
                new CashOperationsRepository(
                    new AzureTableStorage<CashInOutOperationEntity>(_dbSettings.ClientPersonalInfoConnString, "OperationsCash", _log),
                    new AzureTableStorage<AzureIndex>(_dbSettings.ClientPersonalInfoConnString, "OperationsCash", _log)));

            builder.RegisterInstance<ICashOutAttemptRepository>(
                new CashOutAttemptRepository(
                    new AzureTableStorage<CashOutAttemptEntity>(_dbSettings.BalancesInfoConnString, "CashOutAttempt", _log)));

            builder.RegisterInstance<IClientTradesRepository>(
                new ClientTradesRepository(
                    new AzureTableStorage<ClientTradeEntity>(_dbSettings.HTradesConnString, "Trades", _log)));

            builder.RegisterInstance<ILimitTradeEventsRepository>(
                new LimitTradeEventsRepository(
                    new AzureTableStorage<LimitTradeEventEntity>(_dbSettings.ClientPersonalInfoConnString, "LimitTradeEvents", _log)));

            builder.RegisterInstance<IForwardWithdrawalRepository>(
                new ForwardWithdrawalRepository(
                    new AzureTableStorage<ForwardWithdrawalEntity>(_dbSettings.BalancesInfoConnString, "ForwardWithdrawal", _log)));

            builder.RegisterInstance<ITransferEventsRepository>(
                new TransferEventsRepository(
                    new AzureTableStorage<TransferEventEntity>(_dbSettings.ClientPersonalInfoConnString, "Transfers", _log),
                    new AzureTableStorage<AzureIndex>(_dbSettings.ClientPersonalInfoConnString, "Transfers", _log)));

            builder.RegisterInstance<IChronoBankCommandProducer>(
                new SrvChronoBankCommandProducer(new AzureQueueExt(_dbSettings.ChronoBankSrvConnString, "chronobank-out")));

            builder.RegisterInstance<IClientAccountsRepository>(
                new ClientsRepository(
                    new AzureTableStorage<ClientAccountEntity>(_dbSettings.ClientPersonalInfoConnString, "Traders", _log)));

            builder.RegisterInstance<IClientSettingsRepository>(
                new ClientSettingsRepository(
                    new AzureTableStorage<ClientSettingsEntity>(_dbSettings.ClientPersonalInfoConnString, "TraderSettings", _log)));

            builder.RegisterInstance<IClientCacheRepository>(
                new ClientCacheRepository(
                    new AzureTableStorage<ClientCacheEntity>(_dbSettings.ClientPersonalInfoConnString, "ClientCache", _log)));

            builder.RegisterInstance<IEthClientEventLogs>(
                new EthClientEventLogs(
                    new AzureTableStorage<EthClientEventRecord>(_dbSettings.LwEthLogsConnString, "EthClientEventLogs", _log)));

            builder.RegisterInstance<IEthereumTransactionRequestRepository>(
                new EthereumTransactionRequestRepository(
                    new AzureTableStorage<EthereumTransactionReqEntity>(_dbSettings.BitCoinQueueConnectionString, "EthereumTxRequest", _log)));

            builder.RegisterInstance<IMarketOrdersRepository>(
                new MarketOrdersRepository(new AzureTableStorage<MarketOrderEntity>(_dbSettings.HMarketOrdersConnString, "MarketOrders", _log)));

            builder.RegisterInstance<ILimitOrdersRepository>(
                new LimitOrdersRepository(new AzureTableStorage<LimitOrderEntity>(_dbSettings.HMarketOrdersConnString, "LimitOrders", _log)));

            builder.RegisterInstance<IMarginTradingPaymentLogRepository>(
                new MarginTradingPaymentLogRepository(
                    new AzureTableStorage<MarginTradingPaymentLogEntity>(_dbSettings.LogsConnString, "MarginTradingPaymentsLog", _log)));

            builder.RegisterInstance<IEmailCommandProducer>(
                new EmailCommandProducer(new AzureQueueExt(_dbSettings.ClientPersonalInfoConnString, "emailsqueue")));
            
            builder.RegisterInstance<IOffchainOrdersRepository>(
                new OffchainOrderRepository(
                    new AzureTableStorage<OffchainOrder>(_dbSettings.OffchainConnString, "OffchainOrders", _log)));

            builder.RegisterInstance<IOffchainRequestRepository>(
                new OffchainRequestRepository(
                    new AzureTableStorage<OffchainRequestEntity>(_dbSettings.OffchainConnString, "OffchainRequests", _log)));

            builder.RegisterInstance<IOffchainTransferRepository>(
                new OffchainTransferRepository(
                    new AzureTableStorage<OffchainTransferEntity>(_dbSettings.OffchainConnString, "OffchainTransfers", _log)));

            builder.RegisterInstance<IPaymentTransactionsRepository>(
                new PaymentTransactionsRepository(
                    new AzureTableStorage<PaymentTransactionEntity>(_dbSettings.ClientPersonalInfoConnString, "PaymentTransactions", _log),
                    new AzureTableStorage<AzureMultiIndex>(_dbSettings.ClientPersonalInfoConnString, "PaymentTransactions", _log)));

            builder.RegisterInstance<IQuantaCommandProducer>(
                new SrvQuantaCommandProducer(new AzureQueueExt(_dbSettings.QuantaSrvConnString, "quanta-out")));

            builder.RegisterInstance<ISrvSolarCoinCommandProducer>(
                new SrvSolarCoinCommandProducer(new AzureQueueExt(_dbSettings.SolarCoinConnString, "solar-out")));

            builder.RegisterInstance(new BitcoinTransactionContextBlobStorage(new AzureBlobStorage(_dbSettings.BitCoinQueueConnectionString)))
                .As<IBitcoinTransactionContextBlobStorage>();
        }

        private void BindRabbitMq(ContainerBuilder builder)
        {
            builder.RegisterInstance(_settings.RabbitMq);
            builder.RegisterType<CashInOutQueue>().SingleInstance();
            builder.RegisterType<TransferQueue>().SingleInstance();
            builder.RegisterType<LimitTradeQueue>().SingleInstance().WithParameter(TypedParameter.From(_settings.Ethereum));
            builder.RegisterType<TradeQueue>().SingleInstance().WithParameter(TypedParameter.From(_settings.Ethereum));
            builder.RegisterType<EthereumEventsQueue>().SingleInstance();
        }
    }
}