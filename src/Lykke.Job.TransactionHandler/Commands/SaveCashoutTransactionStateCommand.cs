using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class SaveCashoutTransactionStateCommand
    {
        [ProtoMember(1)]
        public Queues.Models.CashInOutQueueMessage Message { get; set; }

        [ProtoMember(2)]
        public string TransactionId { get; set; }

        [ProtoMember(3)]
        public CashOutCommand Command { get; set; }

        [ProtoMember(4)]
        public CashOutContextData Context { get; set; }
    }
}
