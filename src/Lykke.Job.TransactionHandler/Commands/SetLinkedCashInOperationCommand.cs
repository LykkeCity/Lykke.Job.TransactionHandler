using Lykke.Job.TransactionHandler.Core.Contracts;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class SetLinkedCashInOperationCommand
    {
        [ProtoMember(1)]
        public CashInOutQueueMessage Message { get; set; }

        [ProtoMember(2)]
        public string Id { get; set; }
    }
}
