using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class SetLinkedCashInOperationCommand
    {
        [ProtoMember(1)]
        public string ClientId { get; set; }

        [ProtoMember(2)]
        public string Id { get; set; }

        [ProtoMember(3)]
        public string CashInId { get; set; }
    }
}
