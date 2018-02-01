using MessagePack;
using Lykke.Job.TransactionHandler.Core.Contracts;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class ForwardWithdawalLinkedEvent
    {
        public CashInOutQueueMessage Message { get; set; }
    }
}
