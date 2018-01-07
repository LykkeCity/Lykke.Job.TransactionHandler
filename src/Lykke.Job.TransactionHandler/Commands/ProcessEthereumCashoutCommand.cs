using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class ProcessEthereumCashoutCommand : ProcessCashOutBaseCommand
    {
        [ProtoMember(4)]
        public string ClientId { get; set; }

        [ProtoMember(5)]
        public string AssetId { get; set; }

        [ProtoMember(6)]
        public string CashOperationId { get; set; }
    }
}
