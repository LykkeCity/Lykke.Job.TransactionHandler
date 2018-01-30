using System.Collections.Generic;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Events.LimitOrders
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class LimitOrderSavedEvent
    {
        public string Id { get; set; }
        public string ClientId { get; set; }
        public bool IsTrustedClient { get; set; }
        public IEnumerable<string> ActiveLimitOrders { get; set; }
    }
}