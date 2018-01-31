using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class TradeCreatedEvent
    {
        public string OrderId { get; set; }

        public bool IsTrustedClient { get; set; }

        public TradeQueueItem.MarketOrder MarketOrder { get; set; }

        public ClientTrade[] ClientTrades { get; set; }

        public TradeQueueItem QueueMessage { get; set; }
    }
}