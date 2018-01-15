using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events
{
    [ProtoContract]
    public class TradeCreatedEvent
    {
        [ProtoMember(1)]
        public string OrderId { get; set; }
        [ProtoMember(2)]
        public bool IsTrustedClient { get; set; }
        [ProtoMember(3)]
        public TradeQueueItem.MarketOrder MarketOrder { get; set; }               
        [ProtoMember(4)]
        public ClientTrade[] ClientTrades { get; set; }

        [ProtoMember(5)]
        public TradeQueueItem QueueMessage { get; set; }
    }
}