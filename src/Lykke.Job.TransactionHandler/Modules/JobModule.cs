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
using Lykke.Bitcoin.Api.Client;
using Lykke.Bitcoin.Api.Client.BitcoinApi;
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
using Lykke.Job.TransactionHandler.Services.ChronoBank;
using Lykke.Job.TransactionHandler.Services.Ethereum;
using Lykke.Job.TransactionHandler.AzureRepositories.Fee;
using Lykke.Job.TransactionHandler.Core.Domain.Fee;
using Lykke.Job.TransactionHandler.Services.Http;
using Lykke.Job.TransactionHandler.Services.MarginTrading;
using Lykke.Job.TransactionHandler.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Services.Notifications;
using Lykke.Job.TransactionHandler.Services.Offchain;
using Lykke.Job.TransactionHandler.Services.Quanta;
using Lykke.Job.TransactionHandler.Services.SolarCoin;
using Lykke.MatchingEngine.Connector.Services;
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
            
            _services.RegisterAssetsClient(AssetServiceSettings.Create(new Uri(_settings.Assets.ServiceUrl), _jobSettings.AssetsCache.ExpirationPeriod));

            Mapper.Initialize(cfg =>
            {
                cfg.CreateMap<IBcnCredentialsRecord, BcnCredentialsRecordEntity>().IgnoreTableEntityFields();
                cfg.AddProfile<ClientAutomapperProfile>();
                cfg.CreateMap<IEthereumTransactionRequest, EthereumTransactionReqEntity>().IgnoreTableEntityFields()
                    .ForMember(x => x.SignedTransferVal, config => config.Ignore())
                    .ForMember(x => x.OperationIdsVal, config => config.Ignore());
            });            
            
            Mapper.Configuration.AssertConfigurationIsValid();

            BindRabbitMq(builder);
            BindMatchingEngineChannel(builder);
            BindRepositories(builder);
            BindServices(builder);
            BindCachedDicts(builder);
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

            builder.RegisterType<OffchainRequestService>().As<IOffchainRequestService>();
            builder.RegisterType<SrvSlackNotifications>()
                .SingleInstance()
                .WithParameter(TypedParameter.From(_settings.SlackIntegration));

            var exchangeOperationsService = new ExchangeOperationsServiceClient(_jobSettings.ExchangeOperationsServiceUrl);
            builder.RegisterInstance(exchangeOperationsService).As<IExchangeOperationsServiceClient>().SingleInstance();

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

            builder.RegisterOperationsRepositoryClients(_settings.OperationsRepositoryServiceClient, _log);

            builder.RegisterBitcoinApiClient(_settings.BitCoinCore.BitcoinCoreApiUrl);
        }

        private void BindRepositories(ContainerBuilder builder)
        {
            builder.RegisterInstance<IAssetSettingRepository>(
                new AssetSettingRepository(
                    AzureTableStorage<AssetSettingEntity>.Create(_dbSettingsManager.ConnectionString(x => x.DictsConnString), "AssetSettings", _log)));

            builder.RegisterInstance<IBitcoinCommandSender>(
                new BitcoinCommandSender(
                    AzureQueueExt.Create(_dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString), "intransactions")));

            builder.RegisterInstance<IBitCoinTransactionsRepository>(
                new BitCoinTransactionsRepository(
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

            builder.RegisterInstance<IMarketOrdersRepository>(
                new MarketOrdersRepository(AzureTableStorage<MarketOrderEntity>.Create(_dbSettingsManager.ConnectionString(x => x.HMarketOrdersConnString), "MarketOrders", _log)));

            builder.RegisterInstance<ILimitOrdersRepository>(
                new LimitOrdersRepository(AzureTableStorage<LimitOrderEntity>.Create(_dbSettingsManager.ConnectionString(x => x.HMarketOrdersConnString), "LimitOrders", _log)));

            builder.RegisterInstance<IMarginTradingPaymentLogRepository>(
                new MarginTradingPaymentLogRepository(
                    AzureTableStorage<MarginTradingPaymentLogEntity>.Create(_dbSettingsManager.ConnectionString(x => x.LogsConnString), "MarginTradingPaymentsLog", _log)));

            builder.RegisterInstance<IEmailCommandProducer>(
                new EmailCommandProducer(AzureQueueExt.Create(_dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "emailsqueue")));

            builder.RegisterInstance<IOffchainOrdersRepository>(
                new OffchainOrderRepository(
                    AzureTableStorage<OffchainOrder>.Create(_dbSettingsManager.ConnectionString(x => x.OffchainConnString), "OffchainOrders", _log)));

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
                      _dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "EthererumPendingActions", _log)));

            builder.RegisterInstance<IFeeLogRepository>(
                new FeeLogRepository(AzureTableStorage<FeeLogEntity>.Create(
                    _dbSettingsManager.ConnectionString(x => x.FeeLogsConnString), "OrdersFeeLog", _log)));
        }

        private void BindRabbitMq(ContainerBuilder builder)
        {
            builder.RegisterInstance(_settings.RabbitMq);
            builder.RegisterType<CashInOutQueue>().SingleInstance().WithParameter(TypedParameter.From(_settings.Ethereum));
            builder.RegisterType<TransferQueue>().SingleInstance().WithParameter(TypedParameter.From(_settings.Ethereum));
            builder.RegisterType<LimitTradeQueue>().SingleInstance().WithParameter(TypedParameter.From(_settings.Ethereum));
            builder.RegisterType<TradeQueue>().SingleInstance().WithParameter(TypedParameter.From(_settings.Ethereum));
            builder.RegisterType<EthereumEventsQueue>().SingleInstance();
        }
    }
}