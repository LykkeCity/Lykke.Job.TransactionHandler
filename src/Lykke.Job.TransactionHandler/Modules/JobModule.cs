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
using Lykke.Job.TransactionHandler.AzureRepositories.Clients;
using Lykke.Job.TransactionHandler.AzureRepositories.Ethereum;
using Lykke.Job.TransactionHandler.AzureRepositories.Exchange;
using Lykke.Job.TransactionHandler.AzureRepositories.Messages.Email;
using Lykke.Job.TransactionHandler.AzureRepositories.Offchain;
using Lykke.Job.TransactionHandler.AzureRepositories.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Core.Domain.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Services;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Fee;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.Sender;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Queues;
using Lykke.Job.TransactionHandler.Services;
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
using Microsoft.Extensions.DependencyInjection;
using System;

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

            _services.RegisterAssetsClient(
                AssetServiceSettings.Create(new Uri(_settings.Assets.ServiceUrl), _jobSettings.AssetsCache.ExpirationPeriod),
                _log,
                true);

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
            builder.RegisterType<OffchainRequestService>().As<IOffchainRequestService>();

            builder.RegisterExchangeOperationsClient(_jobSettings.ExchangeOperationsServiceUrl);

            builder.Register<IEthereumCoreAPI>(x =>
            {
                var api = new EthereumCoreAPI(new Uri(_settings.Ethereum.EthereumCoreUrl));
                api.SetRetryPolicy(null);
                return api;
            }).SingleInstance();

            builder.RegisterType<SrvEthereumHelper>().As<ISrvEthereumHelper>().SingleInstance();

            builder.RegisterType<EmailSender>().As<IEmailSender>().SingleInstance();
            builder.RegisterType<SrvEmailsFacade>().As<ISrvEmailsFacade>().SingleInstance();

            builder.RegisterType<TransactionService>().As<ITransactionService>().SingleInstance();

            builder.RegisterOperationsRepositoryClients(_settings.OperationsRepositoryServiceClient, _log);

            builder.RegisterBitcoinApiClient(_settings.BitCoinCore.BitcoinCoreApiUrl);

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

            builder.RegisterInstance<IClientSettingsRepository>(
                new ClientSettingsRepository(
                    AzureTableStorage<ClientSettingsEntity>.Create(_dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "TraderSettings", _log)));

            builder.RegisterInstance<IClientCacheRepository>(
                new ClientCacheRepository(
                    AzureTableStorage<ClientCacheEntity>.Create(_dbSettingsManager.ConnectionString(x => x.ClientPersonalInfoConnString), "ClientCache", _log)));

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

            builder.RegisterInstance(new BitcoinTransactionContextBlobStorage(AzureBlobStorage.Create(_dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString))))
                .As<IBitcoinTransactionContextBlobStorage>();

            builder.RegisterInstance<IEthererumPendingActionsRepository>(
              new EthererumPendingActionsRepository(
                  AzureTableStorage<EthererumPendingActionEntity>.Create(
                      _dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString), "EthererumPendingActions", _log)));

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
