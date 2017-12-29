using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Services.Ethereum;
using Lykke.MatchingEngine.Connector.Abstractions.Models;
using Lykke.MatchingEngine.Connector.Abstractions.Services;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class EthereumEventsQueue : IQueueSubscriber
    {
        private const string QueueName = "lykke.transactionhandler.ethereum.events";
        private const string HotWalletQueueName = "lykke.transactionhandler.ethereum.hotwallet.events";

        private readonly ILog _log;
        private readonly IMatchingEngineClient _matchingEngineClient;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly IPaymentTransactionsRepository _paymentTransactionsRepository;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly ITradeOperationsRepositoryClient _clientTradesRepositoryClient;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly ITransferOperationsRepositoryClient _transferEventsRepositoryClient;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;
        private readonly IAssetsService _assetsService;
        private readonly IEthererumPendingActionsRepository _ethererumPendingActionsRepository;
        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<CoinEvent> _subscriber;
        private RabbitMqSubscriber<Lykke.Job.EthereumCore.Contracts.Events.HotWalletEvent> _subscriberHotWallet;

        public EthereumEventsQueue(AppSettings.RabbitMqSettings config, ILog log,
            IMatchingEngineClient matchingEngineClient,
            ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            IClientAccountClient clientAccountClient,
            ISrvEmailsFacade srvEmailsFacade,
            IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            IPaymentTransactionsRepository paymentTransactionsRepository,
            IWalletCredentialsRepository walletCredentialsRepository,
            ITradeOperationsRepositoryClient clientTradesRepositoryClient,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            ITransferOperationsRepositoryClient transferEventsRepositoryClient,
            IAssetsServiceWithCache assetsServiceWithCache,
            IBitcoinTransactionService bitcoinTransactionService,
            IAssetsService assetsService,
            IEthererumPendingActionsRepository ethererumPendingActionsRepository)
        {
            _log = log;
            _matchingEngineClient = matchingEngineClient;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
            _clientAccountClient = clientAccountClient;
            _srvEmailsFacade = srvEmailsFacade;
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository;
            _paymentTransactionsRepository = paymentTransactionsRepository;
            _walletCredentialsRepository = walletCredentialsRepository;
            _clientTradesRepositoryClient = clientTradesRepositoryClient;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _assetsServiceWithCache = assetsServiceWithCache;
            _rabbitConfig = config;
            _transferEventsRepositoryClient = transferEventsRepositoryClient;
            _bitcoinTransactionService = bitcoinTransactionService;
            _assetsService = assetsService;
            _ethererumPendingActionsRepository = ethererumPendingActionsRepository;
        }

        public void Start()
        {
            {
                var settings = new RabbitMqSubscriptionSettings
                {
                    ConnectionString = _rabbitConfig.ConnectionString,
                    QueueName = QueueName,
                    ExchangeName = _rabbitConfig.ExchangeEthereumCashIn,
                    DeadLetterExchangeName = $"{_rabbitConfig.ExchangeEthereumCashIn}.dlx",
                    RoutingKey = "",
                    IsDurable = true
                };

                try
                {
                    _subscriber = new RabbitMqSubscriber<CoinEvent>(settings, new DeadQueueErrorHandlingStrategy(_log, settings))
                        .SetMessageDeserializer(new JsonMessageDeserializer<CoinEvent>())
                        .SetMessageReadStrategy(new MessageReadQueueStrategy())
                        .Subscribe(ProcessMessage)
                        .CreateDefaultBinding()
                        .SetLogger(_log)
                        .Start();
                }
                catch (Exception ex)
                {
                    _log.WriteErrorAsync(nameof(EthereumEventsQueue), nameof(Start), null, ex).Wait();
                    throw;
                }
            }

            #region HotWallet

            {
                string exchangeName = $"{_rabbitConfig.ExchangeEthereumCashIn}.hotwallet";
                var settings = new RabbitMqSubscriptionSettings
                {
                    ConnectionString = _rabbitConfig.ConnectionString,
                    QueueName = HotWalletQueueName,
                    ExchangeName = exchangeName,
                    DeadLetterExchangeName = $"{exchangeName}.dlx",
                    RoutingKey = "",
                    IsDurable = true
                };

                try
                {
                    _subscriberHotWallet = new RabbitMqSubscriber<Lykke.Job.EthereumCore.Contracts.Events.HotWalletEvent>(settings,
                        new DeadQueueErrorHandlingStrategy(_log, settings))
                        .SetMessageDeserializer(new JsonMessageDeserializer<Lykke.Job.EthereumCore.Contracts.Events.HotWalletEvent>())
                        .SetMessageReadStrategy(new MessageReadQueueStrategy())
                        .Subscribe(ProcessHotWalletMessage)
                        .CreateDefaultBinding()
                        .SetLogger(_log)
                        .Start();
                }
                catch (Exception ex)
                {
                    _log.WriteErrorAsync(nameof(EthereumEventsQueue), nameof(Start), null, ex).Wait();
                    throw;
                }
            }

            #endregion

        }

        public void Stop()
        {
            _subscriber?.Stop();
            _subscriberHotWallet?.Stop();
        }

        public async Task<bool> ProcessHotWalletMessage(Lykke.Job.EthereumCore.Contracts.Events.HotWalletEvent queueMessage)
        {
            switch (queueMessage.EventType)
            {
                case EthereumCore.Contracts.Enums.HotWalletEventType.CashinCompleted:
                    await ProcessHotWalletCashin(queueMessage);
                    break;

                case EthereumCore.Contracts.Enums.HotWalletEventType.CashoutCompleted:
                    await ProcessHotWalletCashout(queueMessage);
                    break;

                default:
                    break;
            }

            return true;
        }

        public async Task<bool> ProcessMessage(CoinEvent queueMessage)
        {
            switch (queueMessage.CoinEventType)
            {
                case CoinEventType.CashinCompleted:
                    return await ProcessCashIn(queueMessage);
                case CoinEventType.TransferCompleted:
                case CoinEventType.CashoutCompleted:
                    return await ProcessOutcomeOperation(queueMessage);
                default:
                    return true;
            }
        }

        private async Task<bool> ProcessHotWalletCashin(Lykke.Job.EthereumCore.Contracts.Events.HotWalletEvent queueMessage)
        {
            var bcnCreds = await _bcnClientCredentialsRepository.GetByAssetAddressAsync(queueMessage.FromAddress);
            string tokenAddress = queueMessage.TokenAddress;
            var token = await _assetsService.Erc20TokenGetByAddressAsync(tokenAddress);
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(token.AssetId);
            var amount = EthServiceHelpers.ConvertFromContract(queueMessage.Amount, asset.MultiplierPower, asset.Accuracy);

            await HandleCashInOperation(asset, (double)amount, bcnCreds.ClientId, bcnCreds.Address,
                queueMessage.TransactionHash);

            return true;
        }

        private async Task<bool> ProcessHotWalletCashout(Lykke.Job.EthereumCore.Contracts.Events.HotWalletEvent queueMessage)
        {
            string transactionId = queueMessage.OperationId;
            CashOutContextData context = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(transactionId);
            string clientId = context.ClientId;
            string hash = queueMessage.TransactionHash;
            string cashOperationId = context.CashOperationId;

            await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(clientId, cashOperationId, hash);

            return true;
        }

        private async Task<bool> ProcessOutcomeOperation(CoinEvent queueMessage)
        {
            var transferTx = await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(queueMessage.OperationId));
            CashOutContextData context = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(queueMessage.OperationId);


            if (transferTx != null)
            {
                switch (transferTx.OperationType)
                {
                    case OperationType.CashOut:
                        await SetCashoutHashes(transferTx, queueMessage.TransactionHash);
                        break;
                    case OperationType.Trade:
                        await SetTradeHashes(transferTx, queueMessage.TransactionHash);
                        break;
                    case OperationType.TransferToTrusted:
                    case OperationType.TransferFromTrusted:
                        await SetTransferHashes(transferTx, queueMessage.TransactionHash);
                        break;
                }

                return true;
            }

            if (context != null)
            {
                string clientId = context.ClientId;
                string hash = queueMessage.TransactionHash;
                string cashOperationId = context.CashOperationId;

                await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(clientId, cashOperationId, hash);
            }


            return true;
        }

        private async Task SetTradeHashes(IEthereumTransactionRequest txRequest, string hash)
        {
            foreach (var id in txRequest.OperationIds)
            {
                await _clientTradesRepositoryClient.UpdateBlockchainHashAsync(txRequest.ClientId, id, hash);
            }
        }

        private async Task SetCashoutHashes(IEthereumTransactionRequest txRequest, string hash)
        {
            foreach (var id in txRequest.OperationIds)
            {
                await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(txRequest.ClientId, id, hash);
            }
        }

        private async Task SetTransferHashes(IEthereumTransactionRequest txRequest, string hash)
        {
            foreach (var id in txRequest.OperationIds)
            {
                await _transferEventsRepositoryClient.UpdateBlockChainHashAsync(txRequest.ClientId, id, hash);
            }
        }

        private async Task<bool> ProcessCashIn(CoinEvent queueMessage)
        {
            if (queueMessage.CoinEventType != CoinEventType.CashinCompleted)
                return true;

            var bcnCreds = await _bcnClientCredentialsRepository.GetByAssetAddressAsync(queueMessage.FromAddress);
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(bcnCreds.AssetId);
            var amount = EthServiceHelpers.ConvertFromContract(queueMessage.Amount, asset.MultiplierPower, asset.Accuracy);

            await HandleCashInOperation(asset, (double)amount, bcnCreds.ClientId, bcnCreds.Address,
                queueMessage.TransactionHash, createPendingActions: true);

            return true;
        }

        public async Task HandleCashInOperation(Asset asset, double amount, string clientId, string clientAddress, string hash, bool createPendingActions = false)
        {
            var id = Guid.NewGuid().ToString("N");

            var pt = await _paymentTransactionsRepository.TryCreateAsync(PaymentTransaction.Create(hash,
                CashInPaymentSystem.Ethereum, clientId, amount,
                asset.DisplayId ?? asset.Id, status: PaymentStatus.Processing));
            if (pt == null)
            {
                await
                    _log.WriteWarningAsync(nameof(EthereumEventsQueue), nameof(HandleCashInOperation), hash,
                        "Transaction already handled");
                //return if was handled previously
                return;
            }

            if (createPendingActions)
            {
                await _ethererumPendingActionsRepository.CreateAsync(clientId, Guid.NewGuid().ToString());
            }

            var result = await _matchingEngineClient.CashInOutAsync(id, clientId, asset.Id, amount);

            if (result == null || result.Status != MeStatusCodes.Ok)
            {
                await
                    _log.WriteWarningAsync(nameof(EthereumEventsQueue), nameof(HandleCashInOperation), "ME error",
                        result.ToJson());
            }
            else
            {
                var walletCreds = await _walletCredentialsRepository.GetAsync(clientId);
                await _cashOperationsRepositoryClient.RegisterAsync(new CashInOutOperation
                {
                    Id = id,
                    ClientId = clientId,
                    Multisig = walletCreds.MultiSig,
                    AssetId = asset.Id,
                    Amount = amount,
                    BlockChainHash = hash,
                    DateTime = DateTime.UtcNow,
                    AddressTo = clientAddress,
                    State = TransactionStates.SettledOnchain
                });

                var clientAcc = await _clientAccountClient.GetByIdAsync(clientId);
                await _srvEmailsFacade.SendNoRefundDepositDoneMail(clientAcc.PartnerId, clientAcc.Email, amount, asset.Id);

                await _paymentTransactionsRepository.SetStatus(hash, PaymentStatus.NotifyProcessed);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    #region Models

    public interface ICoinEvent
    {
        string OperationId { get; }
        CoinEventType CoinEventType { get; set; }
        string TransactionHash { get; }
        string ContractAddress { get; }
        string FromAddress { get; }
        string ToAddress { get; }
        string Amount { get; }
        string Additional { get; }
        DateTime EventTime { get; }
        bool Success { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CoinEventType
    {
        CashinStarted,
        CashinCompleted,
        CashoutStarted,
        CashoutCompleted,
        TransferStarted,
        TransferCompleted
    }

    public class CoinEvent : ICoinEvent
    {
        public string OperationId { get; set; }
        public CoinEventType CoinEventType { get; set; }
        public string TransactionHash { get; set; }
        public string ContractAddress { get; set; }
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public string Amount { get; set; }
        public string Additional { get; set; }
        public DateTime EventTime { get; set; }
        public bool Success { get; set; }
    }

    #endregion
}