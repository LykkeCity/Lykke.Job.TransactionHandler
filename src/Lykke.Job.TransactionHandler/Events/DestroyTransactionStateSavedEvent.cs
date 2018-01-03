using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events
{
    [ProtoContract]
    public class DestroyTransactionStateSavedEvent
    {
        [ProtoMember(1)]
        public Core.Domain.BitCoin.DestroyCommand Command { get; set; }
    }
}
