using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class SendBitcoinCommand
    {
        [ProtoMember(1)]
        public Core.Domain.BitCoin.DestroyCommand Command { get; set; }
    }
}
