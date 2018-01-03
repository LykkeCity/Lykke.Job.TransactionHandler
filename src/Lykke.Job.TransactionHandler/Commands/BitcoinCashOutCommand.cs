using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class BitcoinCashOutCommand : ProcessCashOutBaseCommand
    {
        [ProtoMember(4)]
        public string AssetId { get; set; }
    }
}
