using Lykke.Job.TransactionHandler.Core.Contracts;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class TransferOperationStateSavedEvent
    {
        public string TransactionId { get; set; }

        public TransferQueueMessage QueueMessage { get; set; }

        public double AmountNoFee { get; set; }
    }
}