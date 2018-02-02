using System;

namespace Lykke.Job.TransactionHandler.Core.Domain.Exchange
{
    public enum OrderType
    {
        Buy, Sell
    }

    public enum OrderStatus
    {
        /// <summary>
        /// Initial status, limit order in order book
        /// </summary>
        InOrderBook,
        /// <summary>
        /// Partially matched
        /// </summary>
        Processing,
        /// <summary>
        /// Fully matched
        /// </summary>
        Matched,
        /// <summary>
        /// Not enough funds on account
        /// </summary>
        NotEnoughFunds,
        /// <summary>
        /// Reserved volume greater than balance
        /// </summary>
        ReservedVolumeGreaterThanBalance,
        /// <summary>
        /// No liquidity
        /// </summary>
        NoLiquidity,
        /// <summary>
        /// Unknown asset
        /// </summary>
        UnknownAsset,
        /// <summary>
        /// Cancelled
        /// </summary>
        Cancelled,
        /// <summary>
        /// Lead to negative spread
        /// </summary>
        LeadToNegativeSpread,
        /// <summary>
        /// Invalid fee
        /// </summary>
        InvalidFee,
        /// <summary>
        /// Too small volume
        /// </summary>
        TooSmallVolume
    }

    public interface IOrderBase
    {
        string Id { get; }
        string ClientId { get; set; }
        DateTime CreatedAt { get; set; }
        double Volume { get; set; }
        double Price { get; set; }
        string AssetPairId { get; set; }
        string Status { get; set; }
        bool Straight { get; set; }
    }

}