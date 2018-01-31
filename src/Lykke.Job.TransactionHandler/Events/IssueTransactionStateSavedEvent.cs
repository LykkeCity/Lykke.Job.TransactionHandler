using MessagePack;
using Lykke.Job.TransactionHandler.Core.Contracts;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class IssueTransactionStateSavedEvent
    {
        public CashInOutQueueMessage Message { get; set; }

        public Core.Domain.BitCoin.IssueCommand Command { get; set; }
    }
}
