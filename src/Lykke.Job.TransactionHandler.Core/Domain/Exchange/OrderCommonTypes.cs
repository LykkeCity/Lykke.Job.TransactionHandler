using System;

namespace Lykke.Job.TransactionHandler.Core.Domain.Exchange
{
    public enum OrderType
    {
        Buy, Sell
    }

    public enum OrderStatus
    {
        //Init status, limit order in order book
        InOrderBook
        //Partially matched
        , Processing
        //Fully matched
        , Matched
        //Not enough funds on account
        , NotEnoughFunds
        //Reserved volume greater than balance
        , ReservedVolumeGreaterThanBalance
        //No liquidity
        , NoLiquidity
        //Unknown asset
        , UnknownAsset
        //One of trades or whole order has volume/price*volume less then configured dust
        , Dust
        //Cancelled
        , Cancelled
        // negative spread 
        , LeadToNegativeSpread
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