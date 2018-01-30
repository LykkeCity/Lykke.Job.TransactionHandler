using Lykke.Job.TransactionHandler.Core.Contracts;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class SaveManualOperationStateCommand
    {
        public CashInOutQueueMessage Message { get; set; }
    }
}
