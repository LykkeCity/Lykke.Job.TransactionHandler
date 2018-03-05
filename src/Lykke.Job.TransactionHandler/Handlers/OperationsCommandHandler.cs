using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Fee;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Operations.Client;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class OperationsCommandHandler
    {
        private readonly ILog _log;
        private readonly ITransactionsRepository _transactionsRepository;
        private readonly ITransactionService _transactionService;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IFeeCalculationService _feeCalculationService;
        private readonly IOperationsClient _operationsClient;
        private readonly TimeSpan _retryTimeout;

        public OperationsCommandHandler(
            [NotNull] ILog log,
            [NotNull] ITransactionsRepository transactionsRepository,
            [NotNull] ITransactionService transactionService,
            [NotNull] IWalletCredentialsRepository walletCredentialsRepository,
            [NotNull] IFeeCalculationService feeCalculationService,
            [NotNull] IOperationsClient operationsClient,
            TimeSpan retryTimeout)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _transactionsRepository = transactionsRepository ?? throw new ArgumentNullException(nameof(transactionsRepository));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _walletCredentialsRepository = walletCredentialsRepository ?? throw new ArgumentNullException(nameof(walletCredentialsRepository));
            _feeCalculationService = feeCalculationService ?? throw new ArgumentNullException(nameof(feeCalculationService));
            _operationsClient = operationsClient ?? throw new ArgumentNullException(nameof(operationsClient));
            _retryTimeout = retryTimeout;
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

        public async Task<CommandHandlingResult> Handle(Commands.SaveTransferOperationStateCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(OperationsCommandHandler), nameof(Commands.SaveTransferOperationStateCommand), command.ToJson());

            var message = command.QueueMessage;
            var transactionId = message.Id;

            var transaction = await _transactionsRepository.FindByTransactionIdAsync(transactionId);
            if (transaction == null)
            {
                await _log.WriteWarningAsync(nameof(OperationsCommandHandler), nameof(Commands.SaveManualOperationStateCommand), command.ToJson(), "unknown transaction");
                return CommandHandlingResult.Fail(_retryTimeout);
            }

            var amountNoFee = await _feeCalculationService.GetAmountNoFeeAsync(message);

            var context = await _transactionService.GetTransactionContext<TransferContextData>(transactionId) ??
                          TransferContextData.Create(
                              message.FromClientId,
                              new TransferContextData.TransferModel
                              {
                                  ClientId = message.ToClientid
                              },
                              new TransferContextData.TransferModel
                              {
                                  ClientId = message.FromClientId
                              });

            context.Transfers[0].OperationId = Guid.NewGuid().ToString();
            context.Transfers[1].OperationId = Guid.NewGuid().ToString();

            var destWallet = await _walletCredentialsRepository.GetAsync(message.ToClientid);
            var sourceWallet = await _walletCredentialsRepository.GetAsync(message.FromClientId);

            var contextJson = context.ToJson();
            var cmd = new TransferCommand
            {
                Amount = amountNoFee,
                AssetId = message.AssetId,
                Context = contextJson,
                SourceAddress = sourceWallet?.MultiSig,
                DestinationAddress = destWallet?.MultiSig,
                TransactionId = Guid.Parse(transactionId)
            };

            await SaveState(cmd, context);

            eventPublisher.PublishEvent(new TransferOperationStateSavedEvent { TransactionId = transactionId, QueueMessage = message, AmountNoFee = (double) amountNoFee });

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

        public async Task<CommandHandlingResult> Handle(Commands.CompleteOperationCommand command, IEventPublisher eventPublisher)
        {
            await _operationsClient.Complete(command.CommandId);

            return CommandHandlingResult.Ok();
        }
    }
}