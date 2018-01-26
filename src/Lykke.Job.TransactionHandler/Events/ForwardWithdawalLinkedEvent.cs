using Lykke.Job.TransactionHandler.Core.Contracts;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events
{
    [ProtoContract]
    public class ForwardWithdawalLinkedEvent
    {
        [ProtoMember(1)]
        public CashInOutQueueMessage Message { get; set; }
    }
}
