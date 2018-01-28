using Lykke.Job.TransactionHandler.Queues.Models;
using MessagePack;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands.LimitTrades
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class CreateOrUpdateLimitOrderCommand
    {       
        public LimitQueueItem.LimitOrder LimitOrder { get; set; }
        
        public bool IsTrustedClient { get; set; }
    }
}