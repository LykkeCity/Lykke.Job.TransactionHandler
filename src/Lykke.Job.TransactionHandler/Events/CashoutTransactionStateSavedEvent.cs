using MessagePack;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class CashoutTransactionStateSavedEvent
    {
        public Queues.Models.CashInOutQueueMessage Message { get; set; }

        public Core.Domain.BitCoin.CashOutCommand Command { get; set; }
    }
}
