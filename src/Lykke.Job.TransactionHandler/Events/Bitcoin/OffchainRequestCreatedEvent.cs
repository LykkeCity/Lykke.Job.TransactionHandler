using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events.Bitcoin
{
    [ProtoContract]
    public class OffchainRequestCreatedEvent
    {
        [ProtoMember(1)]
        public string OrderId { get; set; }
        [ProtoMember(2)]
        public string ClientId { get; set; }
    }
}