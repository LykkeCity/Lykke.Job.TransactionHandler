using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events
{
    [ProtoContract]
    public class CashoutTransactionStateSavedEvent
    {
        [ProtoMember(1)]
        public Queues.Models.CashInOutQueueMessage Message { get; set; }
    }
}
