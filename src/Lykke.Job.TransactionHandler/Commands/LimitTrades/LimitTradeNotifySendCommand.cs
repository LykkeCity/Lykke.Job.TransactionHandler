using System.Collections.Generic;
using Lykke.Job.TransactionHandler.Queues.Models;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands.LimitTrades
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class LimitTradeNotifySendCommand
    {        
        public LimitQueueItem.LimitOrderWithTrades LimitOrder { get; set; }        
        public List<Handlers.AggregatedTransfer> Aggregated { get; set; }        
        public double? PrevRemainingVolume { get; set; }
    }
}