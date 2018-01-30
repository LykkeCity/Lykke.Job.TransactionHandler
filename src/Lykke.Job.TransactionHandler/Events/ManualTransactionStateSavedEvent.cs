using Lykke.Job.TransactionHandler.Core.Contracts;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class ManualTransactionStateSavedEvent
    {
        public CashInOutQueueMessage Message { get; set; }
    }
}
