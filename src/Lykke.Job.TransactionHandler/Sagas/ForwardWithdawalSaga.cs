using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class ForwardWithdawalSaga
    {
        private readonly ILog _log;
        private readonly Core.Services.BitCoin.IBitcoinTransactionService _bitcoinTransactionService;

        public ForwardWithdawalSaga(
            [NotNull] ILog log,
            [NotNull] Core.Services.BitCoin.IBitcoinTransactionService bitcoinTransactionService)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _bitcoinTransactionService = bitcoinTransactionService ?? throw new ArgumentNullException(nameof(bitcoinTransactionService));
        }

        private async Task Handle(CashoutTransactionStateSavedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(ForwardWithdawalSaga), nameof(CashoutTransactionStateSavedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.Message;
            var transactionId = message.Id;
            var context = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(transactionId);

            var isForwardWithdawal = context.AddData?.ForwardWithdrawal != null;
            if (isForwardWithdawal)
            {
                sender.SendCommand(new SetLinkedCashInOperationCommand
                {
                    Message = message,
                    Id = context.AddData.ForwardWithdrawal.Id
                }, "forward-withdrawal");
            }
        }

    }
}
