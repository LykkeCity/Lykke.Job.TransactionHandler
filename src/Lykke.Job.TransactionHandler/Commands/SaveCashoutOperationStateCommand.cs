using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Queues.Models;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class SaveCashoutOperationStateCommand
    {
        public CashInOutQueueMessage Message { get; set; }

        public CashOutCommand Command { get; set; }

        public CashOutContextData Context { get; set; }
    }
}
