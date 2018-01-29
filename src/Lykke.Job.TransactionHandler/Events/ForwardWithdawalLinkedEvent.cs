using MessagePack;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class ForwardWithdawalLinkedEvent
    {
        public Queues.Models.CashInOutQueueMessage Message { get; set; }
    }
}
