using MessagePack;
using Lykke.Job.TransactionHandler.Core.Contracts;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class CashoutTransactionStateSavedEvent
    {
        public CashInOutQueueMessage Message { get; set; }

        public Core.Domain.BitCoin.CashOutCommand Command { get; set; }
    }
}
