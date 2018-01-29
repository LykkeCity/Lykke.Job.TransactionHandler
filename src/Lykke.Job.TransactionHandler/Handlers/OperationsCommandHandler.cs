using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class OperationsCommandHandler
    {
        private readonly ILog _log;
        private readonly ITransactionsRepository _transactionsRepository;
        private readonly ITransactionService _transactionService;

        public OperationsCommandHandler(
            [NotNull] ILog log,
            [NotNull] ITransactionsRepository transactionsRepository,
            [NotNull] ITransactionService transactionService)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _transactionsRepository = transactionsRepository ?? throw new ArgumentNullException(nameof(transactionsRepository));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
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