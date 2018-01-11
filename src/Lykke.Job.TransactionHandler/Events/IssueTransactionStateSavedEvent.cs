using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events
{
    [ProtoContract]
    public class IssueTransactionStateSavedEvent
    {
        [ProtoMember(1)]
        public Queues.Models.CashInOutQueueMessage Message { get; set; }

        [ProtoMember(2)]
        public Core.Domain.BitCoin.IssueCommand Command { get; set; }
    }
}
