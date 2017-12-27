using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Bitcoin.Api.Client.BitcoinApi;
+using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.ChronoBank;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class CashInOutQueue : IQueueSubscriber
    {
        private const string QueueName = "transactions.cashinout";

        private readonly ILog _log;
        private readonly IBitcoinCommandSender _bitcoinCommandSender;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IBitCoinTransactionsRepository _bitcoinTransactionsRepository;
        private readonly IForwardWithdrawalRepository _forwardWithdrawalRepository;
        private readonly IOffchainRequestService _offchainRequestService;
        private readonly IClientSettingsRepository _clientSettingsRepository;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly ISrvEthereumHelper _srvEthereumHelper;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly IEthClientEventLogs _ethClientEventLogs;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;
        private readonly AppSettings.EthereumSettings _settings;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly ISrvSolarCoinHelper _srvSolarCoinHelper;
        private readonly IChronoBankService _chronoBankService;
        private readonly IBitcoinApiClient _bitcoinApiClient;
        private readonly IDeduplicator _deduplicator;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly IBitcoinCashinRepository _bitcoinCashinTypeRepository;

        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<CashInOutQueueMessage> _subscriber;

        public CashInOutQueue(
            AppSettings.RabbitMqSettings config, 
            ILog log,
            IBitcoinCommandSender bitcoinCommandSender,
            ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            IWalletCredentialsRepository walletCredentialsRepository,
            IBitCoinTransactionsRepository bitcoinTransactionsRepository,
            IForwardWithdrawalRepository forwardWithdrawalRepository,
            IOffchainRequestService offchainRequestService,
            IClientSettingsRepository clientSettingsRepository,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            ISrvEthereumHelper srvEthereumHelper,
            IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            IEthClientEventLogs ethClientEventLogs,
            IBitcoinTransactionService bitcoinTransactionService,
            IAssetsServiceWithCache assetsServiceWithCache,
            AppSettings.EthereumSettings settings, 
            IClientAccountClient clientAccountClient, 
            ISrvEmailsFacade srvEmailsFacade, 
            ISrvSolarCoinHelper srvSolarCoinHelper, 
            IChronoBankService chronoBankService, 
            IBitcoinApiClient bitcoinApiClient,
            [NotNull] IDeduplicator deduplicator, IBitcoinCashinRepository bitcoinCashinTypeRepository,
            ICqrsEngine cqrsEngine)
        {
            _rabbitConfig = config;
            _log = log;
            _bitcoinCommandSender = bitcoinCommandSender;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
            _walletCredentialsRepository = walletCredentialsRepository;
            _bitcoinTransactionsRepository = bitcoinTransactionsRepository;
            _forwardWithdrawalRepository = forwardWithdrawalRepository;
            _offchainRequestService = offchainRequestService;
            _clientSettingsRepository = clientSettingsRepository;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _srvEthereumHelper = srvEthereumHelper;
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository;
            _ethClientEventLogs = ethClientEventLogs;
            _bitcoinTransactionService = bitcoinTransactionService;
            _assetsServiceWithCache = assetsServiceWithCache;
            _settings = settings;
            _clientAccountClient = clientAccountClient;
            _srvEmailsFacade = srvEmailsFacade;
            _srvSolarCoinHelper = srvSolarCoinHelper;
            _chronoBankService = chronoBankService;
            _bitcoinApiClient = bitcoinApiClient;
            _bitcoinCashinTypeRepository = bitcoinCashinTypeRepository;
            _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
            _cqrsEngine = cqrsEngine;
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.ConnectionString,
                QueueName = QueueName,
                ExchangeName = _rabbitConfig.ExchangeCashOperation,
                DeadLetterExchangeName = $"{_rabbitConfig.ExchangeCashOperation}.dlx",
                RoutingKey = "",
                IsDurable = true
            };

            try
            {
                _subscriber = new RabbitMqSubscriber<CashInOutQueueMessage>(settings, new DeadQueueErrorHandlingStrategy(_log, settings))
                    .SetMessageDeserializer(new JsonMessageDeserializer<CashInOutQueueMessage>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .Subscribe(ProcessMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(CashInOutQueue), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        public async Task ProcessMessage(CashInOutQueueMessage queueMessage)
        {
            if (!await _deduplicator.EnsureNotDuplicateAsync(queueMessage))
            {
                await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(ProcessMessage), queueMessage.ToJson(), "Duplicated message");
                return false;
            }

            var transaction = await _bitcoinTransactionsRepository.FindByTransactionIdAsync(queueMessage.Id);
            if (transaction == null)
            {
                // external cashin
                if (_cashOperationsRepositoryClient.GetAsync(queueMessage.ClientId, queueMessage.Id) != null)
                {
                    var asset = await _assetsServiceWithCache.TryGetAssetAsync(queueMessage.AssetId);

                    if (!await _clientSettingsRepository.IsOffchainClient(queueMessage.ClientId) || asset.Blockchain != Blockchain.Bitcoin || asset.IsTrusted && asset.Id != LykkeConstants.BitcoinAssetId)
                        return;

                    if (asset.Id == LykkeConstants.BitcoinAssetId)
                    {                        
                        var createOffchainRequestCommand = new CreateOffchainCashoutRequestCommand
                        {
                            Id = Guid.NewGuid().ToString(),
                            ClientId = queueMessage.ClientId,
                            AssetId = queueMessage.AssetId,
                            Amount = queueMessage.Amount.ParseAnyDouble()
                        };

                        _cqrsEngine.SendCommand(createOffchainRequestCommand, "tx-handler", "bitcoin");                        
                    }                    
                }
                else
                {
                    await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(ProcessMessage), queueMessage.ToJson(), "unknown transaction");
                }
            }
            else
            {
                try
                {
                    switch (transaction.CommandType)
                    {
                        case BitCoinCommands.CashIn:
                        case BitCoinCommands.Issue:
                            var issueCommand = new IssueCommand();
                            _cqrsEngine.SendCommand(issueCommand, "tx-handler", "tx-handler");
                            break;                            
                        case BitCoinCommands.CashOut:
                            var cashoutCommand = new CashoutCommand();
                            _cqrsEngine.SendCommand(cashoutCommand, "tx-handler", "tx-handler");
                            break;                                                        
                        case BitCoinCommands.Destroy:
                            var destroyCommand = new DestroyCommand();
                            _cqrsEngine.SendCommand(destroyCommand, "tx-handler", "tx-handler");
                            break;
                        case BitCoinCommands.ManualUpdate:
                            var manualUpdateCommand = new ManualUpdateCommand();
                            _cqrsEngine.SendCommand(manualUpdateCommand, "tx-handler", "tx-handler");
                            break;                            
                        default:
                            await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(ProcessMessage), queueMessage.ToJson(), $"Unknown command type (value = [{transaction.CommandType}])");   
                            break;                            
                    }
                }
            }            
        }        

        public void Dispose()
        {
            Stop();
        }
    }

    public class CreateOffchainCashoutRequestCommand
    {
        public string Id { get; set; }
        public string ClientId { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }
    }

    [ProtoContract]
    public class ManualUpdateCommand
    {
    }

    [ProtoContract]
    public class DestroyCommand
    {
        
    }

    [ProtoContract]
    public class CashoutCommand
    {
    }

    [ProtoContract]
    public class IssueCommand
    {
    }

    [ProtoContract]
    public class ExternalCashInCommand
    {
        public string ClientId { get; set; }
        public string AssetId { get; set; }        
        public double Amount { get; set; }
    }    
}