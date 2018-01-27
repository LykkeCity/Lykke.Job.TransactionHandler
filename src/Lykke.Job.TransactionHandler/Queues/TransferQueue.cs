using System;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.Operations.Client;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using Lykke.Job.TransactionHandler.Core.Services;
using Lykke.Job.TransactionHandler.Core.Services.Fee;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class TransferQueue : IQueueSubscriber
    {
        private const string QueueName = "transactions.transfer";

        private readonly ILog _log;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IBitCoinTransactionsRepository _bitCoinTransactionsRepository;
        private readonly ITransferOperationsRepositoryClient _transferEventsRepositoryClient;
        private readonly IOffchainRequestService _offchainRequestService;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly ISrvEthereumHelper _srvEthereumHelper;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly IOperationsClient _operationsClient;
        private readonly AppSettings.EthereumSettings _settings;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IDeduplicator _deduplicator;
        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private readonly IFeeLogService _feeLogService;
        private RabbitMqSubscriber<TransferQueueMessage> _subscriber;

        public TransferQueue(AppSettings.RabbitMqSettings config, ILog log,
            ITransferOperationsRepositoryClient transferEventsRepositoryClient,
            IWalletCredentialsRepository walletCredentialsRepository,
            IBitCoinTransactionsRepository bitCoinTransactionsRepository,
            IOffchainRequestService offchainRequestService,
            IBitcoinTransactionService bitcoinTransactionService, IClientAccountClient clientAccountClient,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            ISrvEthereumHelper srvEthereumHelper,
            IBcnClientCredentialsRepository bcnClientCredentialsRepository, AppSettings.EthereumSettings settings,
            IOperationsClient operationsClient, IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IDeduplicator deduplicator,
            IFeeLogService feeLogService)
        {
            _rabbitConfig = config;
            _log = log;
            _transferEventsRepositoryClient = transferEventsRepositoryClient;
            _walletCredentialsRepository = walletCredentialsRepository;
            _bitCoinTransactionsRepository = bitCoinTransactionsRepository;
            _offchainRequestService = offchainRequestService;
            _bitcoinTransactionService = bitcoinTransactionService;
            _clientAccountClient = clientAccountClient;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _srvEthereumHelper = srvEthereumHelper;
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository;
            _settings = settings;
            _operationsClient = operationsClient;
            _assetsServiceWithCache = assetsServiceWithCache;
            _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
            _feeLogService = feeLogService ?? throw new ArgumentNullException(nameof(feeLogService));
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.ConnectionString,
                QueueName = QueueName,
                ExchangeName = _rabbitConfig.ExchangeTransfer,
                DeadLetterExchangeName = $"{_rabbitConfig.ExchangeTransfer}.dlx",
                RoutingKey = "",
                IsDurable = true
            };

            try
            {
                _subscriber = new RabbitMqSubscriber<TransferQueueMessage>(settings, new DeadQueueErrorHandlingStrategy(_log, settings))
                    .SetMessageDeserializer(new JsonMessageDeserializer<TransferQueueMessage>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .Subscribe(ProcessMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(TransferQueue), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        public async Task ProcessMessage(TransferQueueMessage queueMessage)
        {
            if (!await _deduplicator.EnsureNotDuplicateAsync(queueMessage))
            {
                await _log.WriteWarningAsync(nameof(TransferQueue), nameof(ProcessMessage), queueMessage.ToJson(), "Duplicated message");
                return;
            }

            await _feeLogService.WriteFeeInfo(queueMessage);

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(queueMessage.AssetId);
            
            var feeAmount = (queueMessage.Fees?.FirstOrDefault()?.Transfer?.Volume ?? 0.0).TruncateDecimalPlaces(asset.Accuracy, true);

            var amount = queueMessage.Amount.ParseAnyDouble();

            amount = amount > 0 ? amount - feeAmount : amount + feeAmount;

            //Get eth request if it is ETH transfer
            var ethTxRequest = await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(queueMessage.Id));

            //Get client wallets
            var toWallet = await _walletCredentialsRepository.GetAsync(queueMessage.ToClientid);
            var fromWallet = await _walletCredentialsRepository.GetAsync(queueMessage.FromClientId);

            //Register transfer events
            var transferState = ethTxRequest == null
                ? TransactionStates.SettledOffchain
                : ethTxRequest.OperationType == OperationType.TransferBetweenTrusted
                    ? TransactionStates.SettledNoChain
                    : TransactionStates.SettledOnchain;

            var destTransfer =
                await
                    _transferEventsRepositoryClient.RegisterAsync(
                        new TransferEvent
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ClientId = queueMessage.ToClientid,
                            DateTime = DateTime.UtcNow,
                            FromId = null,
                            AssetId = queueMessage.AssetId,
                            Amount = amount,
                            TransactionId = queueMessage.Id,
                            IsHidden = false,
                            AddressFrom = toWallet?.Address,
                            AddressTo = toWallet?.MultiSig,
                            Multisig = toWallet?.MultiSig,
                            IsSettled = false,
                            State = transferState
                        });

            var sourceTransfer =
                await
                    _transferEventsRepositoryClient.RegisterAsync(
                        new TransferEvent
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ClientId = queueMessage.FromClientId,
                            DateTime = DateTime.UtcNow,
                            FromId = null,
                            AssetId = queueMessage.AssetId,
                            Amount = -amount,
                            TransactionId = queueMessage.Id,
                            IsHidden = false,
                            AddressFrom = fromWallet?.Address,
                            AddressTo = fromWallet?.MultiSig,
                            Multisig = fromWallet?.MultiSig,
                            IsSettled = false,
                            State = transferState
                        });

            //Craete or Update transfer context
            var transaction = await _bitCoinTransactionsRepository.FindByTransactionIdAsync(queueMessage.Id);
            if (transaction == null)
            {
                await _log.WriteWarningAsync(nameof(TransferQueue), nameof(ProcessMessage), queueMessage.ToJson(), "unkown transaction");
                return;
            }

            var contextData = await _bitcoinTransactionService.GetTransactionContext<TransferContextData>(transaction.TransactionId);
            if (contextData == null)
            {
                contextData = TransferContextData
                    .Create(queueMessage.FromClientId, new TransferContextData.TransferModel
                    {
                        ClientId = queueMessage.ToClientid
                    }, new TransferContextData.TransferModel
                    {
                        ClientId = queueMessage.FromClientId
                    });
            }

            contextData.Transfers[0].OperationId = destTransfer.Id;
            contextData.Transfers[1].OperationId = sourceTransfer.Id;

            var contextJson = contextData.ToJson();
            var cmd = new TransferCommand
            {
                Amount = amount,
                AssetId = queueMessage.AssetId,
                Context = contextJson,
                SourceAddress = fromWallet?.MultiSig,
                DestinationAddress = toWallet?.MultiSig,
                TransactionId = Guid.Parse(queueMessage.Id)
            };

            await _bitCoinTransactionsRepository.UpdateAsync(transaction.TransactionId, cmd.ToJson(), null, "");

            await _bitcoinTransactionService.SetTransactionContext(transaction.TransactionId, contextData);

            if (!(await _clientAccountClient.IsTrustedAsync(queueMessage.ToClientid)).Value
                        && asset.Blockchain == Blockchain.Bitcoin
                        && !asset.IsTrusted)
            {
                try
                {
                    await _offchainRequestService.CreateOffchainRequestAndNotify(transaction.TransactionId,
                        queueMessage.ToClientid, queueMessage.AssetId, (decimal)amount, null,
                        OffchainTransferType.CashinToClient);
                }
                catch (Exception)
                {
                    await _log.WriteWarningAsync(nameof(TransferQueue), nameof(ProcessMessage), "",
                        $"Transfer already exists {transaction.TransactionId}");
                }
            }

            // handling of ETH transfers to trusted wallets
            if (ethTxRequest != null)
            {
                ethTxRequest.OperationIds = new[] { destTransfer.Id, sourceTransfer.Id };
                await _ethereumTransactionRequestRepository.UpdateAsync(ethTxRequest);

                switch (ethTxRequest.OperationType)
                {
                    case OperationType.TransferToTrusted:
                        await ProcessEthTransferTrustedWallet(ethTxRequest, TransferType.ToTrustedWallet);
                        break;
                    case OperationType.TransferFromTrusted:
                        await ProcessEthTransferTrustedWallet(ethTxRequest, TransferType.FromTrustedWallet);
                        break;
                    case OperationType.TransferBetweenTrusted:
                        await ProcessEthTransferTrustedWallet(ethTxRequest, TransferType.BetweenTrusted);
                        break;
                }
            }
            else
            {
                await _operationsClient.Complete(new Guid(transaction.TransactionId));
            }
        }

        private async Task ProcessEthTransferTrustedWallet(IEthereumTransactionRequest txRequest, TransferType transferType)
        {
            if (transferType == TransferType.BetweenTrusted) return;

            try
            {
                var asset = await _assetsServiceWithCache.TryGetAssetAsync(txRequest.AssetId);
                var clientAddress = await _bcnClientCredentialsRepository.GetClientAddress(txRequest.ClientId);
                var hotWalletAddress = _settings.HotwalletAddress;

                string addressFrom;
                string addressTo;
                Guid transferId;
                string sign;
                switch (transferType)
                {
                    case TransferType.ToTrustedWallet:
                        addressFrom = clientAddress;
                        addressTo = hotWalletAddress;
                        transferId = txRequest.SignedTransfer.Id;
                        sign = txRequest.SignedTransfer.Sign;
                        break;
                    case TransferType.FromTrustedWallet:
                        addressFrom = hotWalletAddress;
                        addressTo = clientAddress;
                        transferId = txRequest.Id;
                        sign = string.Empty;
                        break;
                    default:
                        await _log.WriteErrorAsync(nameof(TransferQueue), nameof(ProcessEthTransferTrustedWallet),
                            "Unknown transfer type", null);
                        return;
                }

                var ethResponse = await _srvEthereumHelper.SendTransferAsync(transferId, sign, asset, addressFrom,
                    addressTo, txRequest.Volume);

                if (ethResponse.HasError)
                {
                    await _log.WriteErrorAsync(nameof(TransferQueue), nameof(ProcessEthTransferTrustedWallet), ethResponse.Error.ToJson(), null);
                    return;
                }

                await _operationsClient.Complete(transferId);
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(TransferQueue), nameof(ProcessEthTransferTrustedWallet), e.Message, e);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}