using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class ForwardWithdawalSaga
    {
        private readonly ILog _log;
        private readonly Core.Services.BitCoin.ITransactionService _transactionService;

        public ForwardWithdawalSaga(
            [NotNull] ILog log,
            [NotNull] Core.Services.BitCoin.ITransactionService transactionService)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        }

        private async Task Handle(CashoutTransactionStateSavedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(ForwardWithdawalSaga), nameof(CashoutTransactionStateSavedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.Message;
            var transactionId = message.Id;
            var context = await _transactionService.GetTransactionContext<CashOutContextData>(transactionId);

            var isForwardWithdawal = context.AddData?.ForwardWithdrawal != null;
            if (isForwardWithdawal)
            {
                sender.SendCommand(new SetLinkedCashInOperationCommand
                {
                    Message = message,
                    Id = context.AddData.ForwardWithdrawal.Id
                }, BoundedContexts.ForwardWithdrawal);
            }
        }

    }
}
