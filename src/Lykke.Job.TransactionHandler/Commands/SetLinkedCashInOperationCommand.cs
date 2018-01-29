using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class SetLinkedCashInOperationCommand
    {
        public Queues.Models.CashInOutQueueMessage Message { get; set; }

        public string Id { get; set; }
    }
}
