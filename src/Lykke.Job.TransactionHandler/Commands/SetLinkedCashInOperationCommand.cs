using MessagePack;
using Lykke.Job.TransactionHandler.Core.Contracts;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class SetLinkedCashInOperationCommand
    {
        public CashInOutQueueMessage Message { get; set; }

        public string Id { get; set; }
    }
}
