using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events
{
    [ProtoContract]
    public class DestroyTransactionStateSavedEvent
    {
        [ProtoMember(1)]
        public Queues.Models.CashInOutQueueMessage Message { get; set; }

        [ProtoMember(2)]
        public Core.Domain.BitCoin.DestroyCommand Command { get; set; }
    }
}
