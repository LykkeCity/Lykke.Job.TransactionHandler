using System.Collections.Generic;
using Lykke.Job.TransactionHandler.Handlers;
using Lykke.Job.TransactionHandler.Queues.Models;
using MessagePack;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events.LimitOrders
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class LimitOrderExecutedEvent
    {        
        public LimitQueueItem.LimitOrderWithTrades LimitOrder { get; set; }        
        public bool IsTrustedClient { get; set; }        
        public bool HasPrevOrderState { get; set; }       
        public double? PrevRemainingVolume { get; set; }        
        public ClientTrade[] Trades { get; set; }        
        public List<Handlers.AggregatedTransfer> Aggregated { get; set; }
    }
}