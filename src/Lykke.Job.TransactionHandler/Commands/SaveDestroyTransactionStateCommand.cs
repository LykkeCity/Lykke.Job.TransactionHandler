using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class SaveDestroyTransactionStateCommand
    {
        [ProtoMember(1)]
        public string TransactionId { get; set; }

        [ProtoMember(2)]
        public DestroyCommand Command { get; set; }

        [ProtoMember(3)]
        public UncolorContextData Context { get; set; }
    }
}
