using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    [ProtoInclude(101, typeof(BitcoinCashOutCommand))]
    [ProtoInclude(102, typeof(ChronoBankCashOutCommand))]
    [ProtoInclude(103, typeof(ProcessEthereumCashoutCommand))]
    [ProtoInclude(104, typeof(SendSolarCashOutCompletedEmailCommand))]
    [ProtoInclude(105, typeof(SolarCashOutCommand))]
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
