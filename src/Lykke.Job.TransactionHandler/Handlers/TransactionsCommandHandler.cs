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
    public class TransactionsCommandHandler
    {
        private readonly ILog _log;
        private readonly IBitCoinTransactionsRepository _bitcoinTransactionsRepository;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;

        public TransactionsCommandHandler(
            [NotNull] ILog log,
            [NotNull] IBitCoinTransactionsRepository bitcoinTransactionsRepository,
            [NotNull] IBitcoinTransactionService bitcoinTransactionService)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _bitcoinTransactionsRepository = bitcoinTransactionsRepository ?? throw new ArgumentNullException(nameof(bitcoinTransactionsRepository));
            _bitcoinTransactionService = bitcoinTransactionService ?? throw new ArgumentNullException(nameof(bitcoinTransactionService));
        }

        public async Task<CommandHandlingResult> Handle(Commands.SaveCashoutTransactionStateCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(TransactionsCommandHandler), nameof(Commands.SaveCashoutTransactionStateCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            await SaveState(command.Command, command.Context);

            eventPublisher.PublishEvent(new CashoutTransactionStateSavedEvent { Message = command.Message, Command = command.Command });

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(Commands.SaveDestroyTransactionStateCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(TransactionsCommandHandler), nameof(Commands.SaveDestroyTransactionStateCommand), command.ToJson(), "");

            await SaveState(command.Command, command.Context);

            eventPublisher.PublishEvent(new DestroyTransactionStateSavedEvent { Message = command.Message, Command = command.Command });

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(Commands.SaveIssueTransactionStateCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(TransactionsCommandHandler), nameof(Commands.SaveIssueTransactionStateCommand), command.ToJson(), "");

            await SaveState(command.Command, command.Context);

            eventPublisher.PublishEvent(new IssueTransactionStateSavedEvent { Message = command.Message, Command = command.Command });

            return CommandHandlingResult.Ok();
        }

        private async Task SaveState(BaseCommand command, BaseContextData context)
        {
            ChaosKitty.Meow();

            var transactionId = command.TransactionId.ToString();
            var requestData = command.ToJson();

            await _bitcoinTransactionsRepository.UpdateAsync(transactionId, requestData, null, "");
            await _bitcoinTransactionService.SetTransactionContext(transactionId, context);
        }
    }
}