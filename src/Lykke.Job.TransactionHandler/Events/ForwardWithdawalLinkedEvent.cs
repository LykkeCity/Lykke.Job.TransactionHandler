using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events
{
    [ProtoContract]
    public class ForwardWithdawalLinkedEvent
    {
        [ProtoMember(1)]
        public Queues.Models.CashInOutQueueMessage Message { get; set; }

        [ProtoMember(2)]
        public string CashInId { get; set; }
    }
}
