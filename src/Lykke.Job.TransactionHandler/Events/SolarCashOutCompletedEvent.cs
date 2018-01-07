using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events
{
    [ProtoContract]
    public class SolarCashOutCompletedEvent
    {
        [ProtoMember(1)]
        public double Amount { get; set; }

        [ProtoMember(2)]
        public string Address { get; set; }

        [ProtoMember(3)]
        public string ClientId { get; set; }
    }
}
