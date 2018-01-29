using System.Collections.Generic;
using Lykke.Job.TransactionHandler.Handlers;
using Lykke.Job.TransactionHandler.Queues.Models;
using MessagePack;
using ProtoBuf;

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