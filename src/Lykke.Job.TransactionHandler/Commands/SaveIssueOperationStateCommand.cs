using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Queues.Models;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class SaveIssueOperationStateCommand
    {
        public CashInOutQueueMessage Message { get; set; }

        public IssueCommand Command { get; set; }

        public IssueContextData Context { get; set; }
    }
}
