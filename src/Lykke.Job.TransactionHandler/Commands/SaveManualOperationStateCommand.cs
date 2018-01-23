using Lykke.Job.TransactionHandler.Queues.Models;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class SaveManualOperationStateCommand
    {
        public CashInOutQueueMessage Message { get; set; }
    }
}
