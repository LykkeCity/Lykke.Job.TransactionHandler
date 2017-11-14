using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Services.Ethereum;
using Lykke.MatchingEngine.Connector.Abstractions.Models;
using Lykke.MatchingEngine.Connector.Abstractions.Services;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Service.Assets.Client.Custom;
using Lykke.Service.ClientAccount.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class EthereumEventsQueue : IQueueSubscriber
    {
        private const string QueueName = "lykke.transactionhandler.ethereum.events";

        private readonly ILog _log;
        private readonly IMatchingEngineClient _matchingEngineClient;
        private readonly ICashOperationsRepository _cashOperationsRepository;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly IPaymentTransactionsRepository _paymentTransactionsRepository;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IClientTradesRepository _clientTradesRepository;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly ICachedAssetsService _assetsService;
        private readonly ITransferEventsRepository _transferEventsRepository;

        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<CoinEvent> _subscriber;

        public EthereumEventsQueue(AppSettings.RabbitMqSettings config, ILog log,
            IMatchingEngineClient matchingEngineClient,
            ICashOperationsRepository cashOperationsRepository,
            IClientAccountClient clientAccountClient,
            ISrvEmailsFacade srvEmailsFacade,
            IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            IPaymentTransactionsRepository paymentTransactionsRepository,
            IWalletCredentialsRepository walletCredentialsRepository,
            IClientTradesRepository clientTradesRepository,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            ICachedAssetsService assetsService,
            ITransferEventsRepository transferEventsRepository)
        {
            _log = log;
            _matchingEngineClient = matchingEngineClient;
            _cashOperationsRepository = cashOperationsRepository;
            _clientAccountClient = clientAccountClient;
            _srvEmailsFacade = srvEmailsFacade;
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository;
            _paymentTransactionsRepository = paymentTransactionsRepository;
            _walletCredentialsRepository = walletCredentialsRepository;
            _clientTradesRepository = clientTradesRepository;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _assetsService = assetsService;
            _rabbitConfig = config;
            _transferEventsRepository = transferEventsRepository;
        }

        public void Start()
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

        public void Stop()
        {
            _subscriber?.Stop();
        }

        public async Task<bool> ProcessMessage(CoinEvent queueMessage)
        {
            await _log.WriteInfoAsync(nameof(EthereumEventsQueue), nameof(ProcessMessage),
                $"queueMessage = {queueMessage.ToJson()}", "ETH event processing started");

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

        private async Task<bool> ProcessOutcomeOperation(CoinEvent queueMessage)
        {
            await _log.WriteInfoAsync(nameof(EthereumEventsQueue), nameof(ProcessOutcomeOperation),
                $"queueMessage = {queueMessage.ToJson()}", "ETH outcome operation processing started");

            var transferTx = await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(queueMessage.OperationId));

            await _log.WriteInfoAsync(nameof(EthereumEventsQueue), nameof(ProcessOutcomeOperation),
                $"transferTx = {transferTx.ToJson()}", "transferTx value");

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
            }

            return true;
        }

        private async Task SetTradeHashes(IEthereumTransactionRequest txRequest, string hash)
        {
            foreach (var id in txRequest.OperationIds)
            {
                await _clientTradesRepository.UpdateBlockChainHashAsync(txRequest.ClientId, id, hash);
            }
        }

        private async Task SetCashoutHashes(IEthereumTransactionRequest txRequest, string hash)
        {
            foreach (var id in txRequest.OperationIds)
            {
                await _cashOperationsRepository.UpdateBlockchainHashAsync(txRequest.ClientId, id, hash);
            }
        }

        private async Task SetTransferHashes(IEthereumTransactionRequest txRequest, string hash)
        {
            await _log.WriteInfoAsync(nameof(EthereumEventsQueue), nameof(SetTransferHashes),
                $"txRequest = {txRequest.ToJson()}, hash = {hash}", "SetTransferHashes started");

            foreach (var id in txRequest.OperationIds)
            {
                await _log.WriteInfoAsync(nameof(EthereumEventsQueue), nameof(SetTransferHashes),
                    $"operationId = {id}", "Going to update hash for transfer operation");

                await _transferEventsRepository.UpdateBlockChainHashAsync(txRequest.ClientId, id, hash);
            }
        }

        private async Task<bool> ProcessCashIn(CoinEvent queueMessage)
        {
            if (queueMessage.CoinEventType != CoinEventType.CashinCompleted)
                return true;

            var bcnCreds = await _bcnClientCredentialsRepository.GetByAssetAddressAsync(queueMessage.FromAddress);

            var asset = await _assetsService.TryGetAssetAsync(bcnCreds.AssetId);
            var amount = EthServiceHelpers.ConvertFromContract(queueMessage.Amount, asset.MultiplierPower, asset.Accuracy);

            await HandleCashInOperation(asset, (double)amount, bcnCreds.ClientId, bcnCreds.Address,
                queueMessage.TransactionHash);

            return true;
        }

        public async Task HandleCashInOperation(IAsset asset, double amount, string clientId, string clientAddress, string hash)
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
                await _cashOperationsRepository.RegisterAsync(new CashInOutOperation
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
                await _srvEmailsFacade.SendNoRefundDepositDoneMail(clientAcc.Email, amount, asset.Id);

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