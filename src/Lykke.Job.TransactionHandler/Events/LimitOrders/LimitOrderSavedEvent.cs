using System.Collections.Generic;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using MessagePack;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events.LimitOrders
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class LimitOrderSavedEvent
    {        
        public string Id { get; set; }        
        public string ClientId { get; set; }        
        public bool IsTrustedClient { get; set; }                
        public IEnumerable<ILimitOrder> ActiveLimitOrders { get; set; }
    }
}