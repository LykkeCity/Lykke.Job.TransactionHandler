using MessagePack;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class IssueTransactionStateSavedEvent
    {
        public Queues.Models.CashInOutQueueMessage Message { get; set; }

        public Core.Domain.BitCoin.IssueCommand Command { get; set; }
    }
}
