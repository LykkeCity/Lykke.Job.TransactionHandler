using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Services.Notifications;
using Lykke.MatchingEngine.Connector.Abstractions.Models;
using Lykke.MatchingEngine.Connector.Abstractions.Services;
using Lykke.Service.Assets.Client.Custom;
using Lykke.Service.ExchangeOperations.Client;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class TransferQueue : IQueueSubscriber
    {
        private const string QueueName = "transactions.transfer";

        private readonly ILog _log;
        private readonly IBitcoinCommandSender _bitcoinCommandSender;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IBitCoinTransactionsRepository _bitCoinTransactionsRepository;
        private readonly ITransferEventsRepository _transferEventsRepository;
        private readonly IOffchainRequestService _offchainRequestService;
        private readonly IClientSettingsRepository _clientSettingsRepository;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;
        private readonly IClientAccountsRepository _clientAccountsRepository;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly ISrvEthereumHelper _srvEthereumHelper;
        private readonly ICachedAssetsService _assetsService;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly AppSettings.EthereumSettings _settings;
        private readonly SrvSlackNotifications _srvSlackNotifications;
        private readonly IMatchingEngineClient _matchingEngineClient;

        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<TransferQueueMessage> _subscriber;

        public TransferQueue(AppSettings.RabbitMqSettings config, ILog log,
            IBitcoinCommandSender bitcoinCommandSender,
            ITransferEventsRepository transferEventsRepository,
            IWalletCredentialsRepository walletCredentialsRepository,
            IBitCoinTransactionsRepository bitCoinTransactionsRepository,
            IOffchainRequestService offchainRequestService, IClientSettingsRepository clientSettingsRepository,
            IBitcoinTransactionService bitcoinTransactionService, IClientAccountsRepository clientAccountsRepository,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            ISrvEthereumHelper srvEthereumHelper, ICachedAssetsService assetsService,
            IBcnClientCredentialsRepository bcnClientCredentialsRepository, AppSettings.EthereumSettings settings,
            SrvSlackNotifications srvSlackNotifications, IMatchingEngineClient matchingEngineClient)
        {
            _rabbitConfig = config;
            _log = log;
            _bitcoinCommandSender = bitcoinCommandSender;
            _transferEventsRepository = transferEventsRepository;
            _walletCredentialsRepository = walletCredentialsRepository;
            _bitCoinTransactionsRepository = bitCoinTransactionsRepository;
            _offchainRequestService = offchainRequestService;
            _clientSettingsRepository = clientSettingsRepository;
            _bitcoinTransactionService = bitcoinTransactionService;
            _clientAccountsRepository = clientAccountsRepository;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _srvEthereumHelper = srvEthereumHelper;
            _assetsService = assetsService;
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository;
            _settings = settings;
            _srvSlackNotifications = srvSlackNotifications;
            _matchingEngineClient = matchingEngineClient;
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

        public async Task<bool> ProcessMessage(TransferQueueMessage queueMessage)
        {
            var ethTxRequest = await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(queueMessage.Id));
            if (ethTxRequest != null && ethTxRequest.OperationType == OperationType.TransferToTrusted)
            {
                return await ProcessEthTransferToTrustedWallet(ethTxRequest);
            }

            var amount = queueMessage.Amount.ParseAnyDouble();

            //Get client wallets
            var toWallet = await _walletCredentialsRepository.GetAsync(queueMessage.ToClientid);
            var fromWallet = await _walletCredentialsRepository.GetAsync(queueMessage.FromClientId);

            //Register transfer events
            var destTransfer =
                await
                    _transferEventsRepository.RegisterAsync(
                        TransferEvent.CreateNew(queueMessage.ToClientid,
                        toWallet.MultiSig, null,
                        queueMessage.AssetId, amount, queueMessage.Id,
                        toWallet.Address, toWallet.MultiSig, state: TransactionStates.SettledOffchain));

            var sourceTransfer =
                await
                    _transferEventsRepository.RegisterAsync(
                        TransferEvent.CreateNew(queueMessage.FromClientId,
                        fromWallet.MultiSig, null,
                        queueMessage.AssetId, -amount, queueMessage.Id,
                        fromWallet.Address, fromWallet.MultiSig, state: TransactionStates.SettledOffchain));

            //Craete or Update transfer context
            var transaction = await _bitCoinTransactionsRepository.FindByTransactionIdAsync(queueMessage.Id);
            if (transaction == null)
            {
                await _log.WriteWarningAsync(nameof(TransferQueue), nameof(ProcessMessage), queueMessage.ToJson(), "unkown transaction");
                return false;
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
                SourceAddress = fromWallet.MultiSig,
                DestinationAddress = toWallet.MultiSig,
                TransactionId = Guid.Parse(queueMessage.Id)
            };

            await _bitCoinTransactionsRepository.UpdateAsync(transaction.TransactionId, cmd.ToJson(), null, "");

            await _bitcoinTransactionService.SetTransactionContext(transaction.TransactionId, contextData);

            if (await _clientSettingsRepository.IsOffchainClient(queueMessage.ToClientid))
            {
                if (!await _clientAccountsRepository.IsTrusted(queueMessage.ToClientid))
                {
                    try
                    {
                        await _offchainRequestService.CreateOffchainRequestAndNotify(transaction.TransactionId, queueMessage.ToClientid, queueMessage.AssetId, (decimal)amount, null, OffchainTransferType.CashinToClient);
                    }
                    catch (Exception)
                    {
                        await _log.WriteWarningAsync(nameof(TransferQueue), nameof(ProcessMessage), "", $"Transfer already exists {transaction.TransactionId}");
                    }
                }
            }
            else
                await _bitcoinCommandSender.SendCommand(cmd);

            return true;
        }

        private async Task<bool> ProcessEthTransferToTrustedWallet(IEthereumTransactionRequest txRequest)
        {
            string ethError = string.Empty;

            try
            {
                var asset = await _assetsService.TryGetAssetAsync(txRequest.AssetId);
                var fromAddress = await _bcnClientCredentialsRepository.GetClientAddress(txRequest.ClientId);

                var ethResponse = await _srvEthereumHelper.SendTransferAsync(txRequest.SignedTransfer.Id,
                    txRequest.SignedTransfer.Sign, asset, fromAddress, _settings.HotwalletAddress,
                    txRequest.Volume);

                if (ethResponse.HasError)
                {
                    ethError = ethResponse.Error.ToJson();
                    await _log.WriteErrorAsync(nameof(TransferQueue), nameof(ProcessEthTransferToTrustedWallet), ethError, null);

                    return false;
                }

                // todo: update txRequest.OperationIds
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(TransferQueue), nameof(ProcessEthTransferToTrustedWallet), e.Message, e);
                ethError = $"{e.GetType()}\n{e.Message}";
            }

            if (!string.IsNullOrEmpty(ethError))
            {
                await _log.WriteErrorAsync(nameof(TransferQueue), nameof(ProcessEthTransferToTrustedWallet), ethError,
                    null);

                return false;
            }

            return true;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}