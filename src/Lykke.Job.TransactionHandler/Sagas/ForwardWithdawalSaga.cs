using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Queues.Models;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class ForwardWithdawalSaga
    {
        private readonly Core.Services.BitCoin.ITransactionService _transactionService;

        public ForwardWithdawalSaga(
            [NotNull] Core.Services.BitCoin.ITransactionService transactionService)
        {
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        }

        public async Task Handle(CashoutTransactionStateSavedEvent evt, ICommandSender sender)
        {
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
