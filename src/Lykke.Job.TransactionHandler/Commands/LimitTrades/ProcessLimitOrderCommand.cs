using Lykke.Job.TransactionHandler.Core.Contracts;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands.LimitTrades
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class ProcessLimitOrderCommand
    {        
        public LimitQueueItem.LimitOrderWithTrades LimitOrder { get; set; }
    }
}