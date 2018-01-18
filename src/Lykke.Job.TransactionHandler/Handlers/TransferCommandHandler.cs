using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Logs;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class TransferCommandHandler
    {
        private readonly ILog _log;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly ITransactionsRepository _transactionsRepository;
        private readonly ITransferOperationsRepositoryClient _transferEventsRepositoryClient;
        private readonly ITransactionService _transactionService;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly ITransferLogRepository _transferLogRepository;

        public TransferCommandHandler(
            [NotNull] ILog log,
            [NotNull] ITransferOperationsRepositoryClient transferEventsRepositoryClient,
            [NotNull] IWalletCredentialsRepository walletCredentialsRepository,
            [NotNull] ITransactionsRepository transactionsRepository,
            [NotNull] ITransactionService transactionService,
            [NotNull] IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] ITransferLogRepository transferLogRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _transferEventsRepositoryClient = transferEventsRepositoryClient ?? throw new ArgumentNullException(nameof(transferEventsRepositoryClient));
            _walletCredentialsRepository = walletCredentialsRepository ?? throw new ArgumentNullException(nameof(walletCredentialsRepository));
            _transactionsRepository = transactionsRepository ?? throw new ArgumentNullException(nameof(transactionsRepository));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository ?? throw new ArgumentNullException(nameof(ethereumTransactionRequestRepository));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _transferLogRepository = transferLogRepository ?? throw new ArgumentNullException(nameof(transferLogRepository));
        }

        public async Task<CommandHandlingResult> Handle(CreateTransferCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(TransferCommandHandler), nameof(CreateTransferCommand), command.ToJson());

            ChaosKitty.Meow();

            var queueMessage = command.QueueMessage;

            await _transferLogRepository.CreateAsync(queueMessage.Id, queueMessage.Date, queueMessage.FromClientId, queueMessage.ToClientid,
                queueMessage.AssetId, queueMessage.Amount, queueMessage.FeeSettings?.ToJson(), queueMessage.FeeData?.ToJson());

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(queueMessage.AssetId);
            var feeAmount = (queueMessage.FeeData?.Amount.ParseAnyDouble() ?? 0.0).TruncateDecimalPlaces(asset.Accuracy, true);

            var amount = queueMessage.Amount.ParseAnyDouble() - feeAmount;
            //Get eth request if it is ETH transfer
            var ethTxRequest = await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(queueMessage.Id));

            //Get client wallets
            var destWallet = await _walletCredentialsRepository.GetAsync(queueMessage.ToClientid);
            var sourceWallet = await _walletCredentialsRepository.GetAsync(queueMessage.FromClientId);

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
                            AddressFrom = destWallet?.Address,
                            AddressTo = destWallet?.MultiSig,
                            Multisig = destWallet?.MultiSig,
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
                            AddressFrom = sourceWallet?.Address,
                            AddressTo = sourceWallet?.MultiSig,
                            Multisig = sourceWallet?.MultiSig,
                            IsSettled = false,
                            State = transferState
                        });

            //Create or Update transfer context
            var transaction = await _transactionsRepository.FindByTransactionIdAsync(queueMessage.Id);
            if (transaction == null)
            {
                await _log.WriteWarningAsync(nameof(TransferCommandHandler), nameof(CreateTransferCommand), queueMessage.ToJson(), "unkown transaction");
                return CommandHandlingResult.Fail(TimeSpan.FromSeconds(20));
            }

            var contextData = await _transactionService.GetTransactionContext<TransferContextData>(transaction.TransactionId);
            if (contextData == null)
            {
                contextData = TransferContextData.Create(
                    queueMessage.FromClientId,
                    new TransferContextData.TransferModel
                    {
                        ClientId = queueMessage.ToClientid
                    },
                    new TransferContextData.TransferModel
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
                SourceAddress = sourceWallet?.MultiSig,
                DestinationAddress = destWallet?.MultiSig,
                TransactionId = Guid.Parse(queueMessage.Id)
            };

            await _transactionsRepository.UpdateAsync(transaction.TransactionId, cmd.ToJson(), null, "");
            await _transactionService.SetTransactionContext(transaction.TransactionId, contextData);

            eventPublisher.PublishEvent(new TransferCreatedEvent
            {
                TransactionId = transaction.TransactionId,
                QueueMessage = queueMessage
            });

            return CommandHandlingResult.Ok();
        }
    }
}
