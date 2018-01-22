using Lykke.Job.TransactionHandler.Queues.Models;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class SaveTransferOperationStateCommand
    {
        [ProtoMember(1)]
        public TransferQueueMessage QueueMessage { get; set; }
    }
}