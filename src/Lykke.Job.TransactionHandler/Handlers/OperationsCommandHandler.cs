using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class OperationsCommandHandler
    {
        private readonly ILog _log;
        private readonly ITransactionsRepository _transactionsRepository;
        private readonly ITransactionService _transactionService;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;

        public OperationsCommandHandler(
            [NotNull] ILog log,
            [NotNull] ITransactionsRepository transactionsRepository,
            [NotNull] ITransactionService transactionService,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IWalletCredentialsRepository walletCredentialsRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _transactionsRepository = transactionsRepository ?? throw new ArgumentNullException(nameof(transactionsRepository));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _walletCredentialsRepository = walletCredentialsRepository ?? throw new ArgumentNullException(nameof(walletCredentialsRepository));
        }

        public async Task<CommandHandlingResult> Handle(Commands.SaveTransferOperationStateCommand operationStateCommand, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(OperationsCommandHandler), nameof(Commands.SaveTransferOperationStateCommand), operationStateCommand.ToJson());

            var message = operationStateCommand.QueueMessage;
            var transactionId = message.Id;

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(message.AssetId);
            var feeAmount = (message.FeeData?.Amount.ParseAnyDouble() ?? 0.0).TruncateDecimalPlaces(asset.Accuracy, true);

            var amount = message.Amount.ParseAnyDouble() - feeAmount;

            var context = await _transactionService.GetTransactionContext<TransferContextData>(transactionId);
            if (context == null)
            {
                context = TransferContextData.Create(
                    message.FromClientId,
                    new TransferContextData.TransferModel
                    {
                        ClientId = message.ToClientid
                    },
                    new TransferContextData.TransferModel
                    {
                        ClientId = message.FromClientId
                    });
            }

            context.Transfers[0].OperationId = Guid.NewGuid().ToString();
            context.Transfers[1].OperationId = Guid.NewGuid().ToString();

            var destWallet = await _walletCredentialsRepository.GetAsync(message.ToClientid);
            var sourceWallet = await _walletCredentialsRepository.GetAsync(message.FromClientId);

            var contextJson = context.ToJson();
            var cmd = new TransferCommand
            {
                Amount = amount,
                AssetId = message.AssetId,
                Context = contextJson,
                SourceAddress = sourceWallet?.MultiSig,
                DestinationAddress = destWallet?.MultiSig,
                TransactionId = Guid.Parse(transactionId)
            };

            await SaveState(cmd, context);

            eventPublisher.PublishEvent(new TransferOperationStateSavedEvent { TransactionId = transactionId, QueueMessage = message, Amount = amount });

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(Commands.SaveCashoutOperationStateCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(OperationsCommandHandler), nameof(Commands.SaveCashoutOperationStateCommand), command.ToJson(), "");

            await SaveState(command.Command, command.Context);

            eventPublisher.PublishEvent(new CashoutTransactionStateSavedEvent { Message = command.Message, Command = command.Command });

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(Commands.SaveIssueOperationStateCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(OperationsCommandHandler), nameof(Commands.SaveIssueOperationStateCommand), command.ToJson(), "");

            await SaveState(command.Command, command.Context);

            eventPublisher.PublishEvent(new IssueTransactionStateSavedEvent { Message = command.Message, Command = command.Command });

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(Commands.SaveManualOperationStateCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(OperationsCommandHandler), nameof(Commands.SaveManualOperationStateCommand), command.ToJson(), "");

            eventPublisher.PublishEvent(new ManualTransactionStateSavedEvent { Message = command.Message });

            return CommandHandlingResult.Ok();
        }

        private async Task SaveState(BaseCommand command, BaseContextData context)
        {
            var transactionId = command.TransactionId.ToString();
            var requestData = command.ToJson();

            await _transactionsRepository.UpdateAsync(transactionId, requestData, null, "");

            ChaosKitty.Meow();

            await _transactionService.SetTransactionContext(transactionId, context);

            ChaosKitty.Meow();
        }
    }
}