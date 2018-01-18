using Lykke.Job.TransactionHandler.Queues.Models;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class SaveManualOperationStateCommand
    {
        [ProtoMember(1)]
        public CashInOutQueueMessage Message { get; set; }
    }
}
