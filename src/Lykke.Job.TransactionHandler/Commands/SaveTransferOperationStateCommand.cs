using Lykke.Job.TransactionHandler.Queues.Models;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class SaveTransferOperationStateCommand
    {
        public TransferQueueMessage QueueMessage { get; set; }
    }
}