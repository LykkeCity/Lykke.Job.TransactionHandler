using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public abstract class ProcessCashOutBaseCommand
    {
        [ProtoMember(1)]
        public string TransactionId { get; set; }

        [ProtoMember(2)]
        public double Amount { get; set; }

        [ProtoMember(3)]
        public string Address { get; set; }
    }
}
