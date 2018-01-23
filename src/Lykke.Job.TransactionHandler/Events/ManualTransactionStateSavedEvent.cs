using MessagePack;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class ManualTransactionStateSavedEvent
    {
        public Queues.Models.CashInOutQueueMessage Message { get; set; }
    }
}
