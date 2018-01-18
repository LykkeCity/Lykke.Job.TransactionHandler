using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events
{
    [ProtoContract]
    public class ManualTransactionStateSavedEvent
    {
        [ProtoMember(1)]
        public Queues.Models.CashInOutQueueMessage Message { get; set; }
    }
}
