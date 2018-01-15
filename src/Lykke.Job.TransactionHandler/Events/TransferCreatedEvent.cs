using Lykke.Job.TransactionHandler.Queues.Models;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events
{
    [ProtoContract]
    public class TransferCreatedEvent
    {
        [ProtoMember(1)]
        public string TransactionId { get; set; }
        [ProtoMember(2)]
        public TransferQueueMessage QueueMessage { get; set; }
    }
}