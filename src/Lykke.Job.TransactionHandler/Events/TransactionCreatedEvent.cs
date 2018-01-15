using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events
{
    [ProtoContract]
    public class TransactionCreatedEvent
    {        
        [ProtoMember(1)]

        public string OrderId { get; set; }
    }
}