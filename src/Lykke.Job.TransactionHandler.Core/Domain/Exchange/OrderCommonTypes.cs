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
        /// Stop limit order pending
        /// </summary>
        Pending,

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
        /// Disabled asset
        /// </summary>
        DisabledAsset,

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
        TooSmallVolume,

        /// <summary>
        /// Invalid price
        /// </summary>
        InvalidPrice,

        /// <summary>
        /// Previous order is not found (by oldUid)
        /// </summary>
        NotFoundPrevious,

        /// <summary>
        /// Replaced
        /// </summary>
        Replaced,

        /// <summary>
        /// Invalid price accuracy
        /// </summary>
        InvalidPriceAccuracy,

        /// <summary>
        /// Invalid volume accuracy
        /// </summary>
        InvalidVolumeAccuracy,

        /// <summary>
        /// Invalid volume
        /// </summary>
        InvalidVolume,

        /// <summary>
        /// Invalid order value
        /// </summary>
        InvalidValue,

        /// <summary>
        /// Too high price deviation
        /// </summary>
        TooHighPriceDeviation
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
