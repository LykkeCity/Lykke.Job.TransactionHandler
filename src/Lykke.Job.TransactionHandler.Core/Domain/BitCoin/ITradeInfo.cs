using System;

namespace Lykke.Job.TransactionHandler.Core.Domain.BitCoin
{
    public interface ITradeInfo
    {
        double? Price { get; set; }
        string LimitOrderId { get; set; }
        string LimitOrderExternalId { get; set; }
        DateTime Timestamp { get; set; }
        string MarketClientId { get; set; }
        string MarketAsset { get; set; }
        double MarketVolume { get; set; }
        string LimitClientId { get; set; }
        double LimitVolume { get; set; }
        string LimitAsset { get; set; }
    }
}