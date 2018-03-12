using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Service.Assets.Client.Models;

namespace Lykke.Job.TransactionHandler.Services
{
    public class EffectivePriceCalculator
    {
        public static double CalcEffectivePrice(List<LimitQueueItem.LimitTradeInfo> trades, AssetPair assetPair, bool isBuy)
        {
            return CalcEffectivePrice(trades.Select(x => new CommonTrade
            {
                Asset = x.Asset,
                OppositeAsset = x.OppositeAsset,
                Volume = x.Volume,
                OppositeVolume = x.OppositeVolume,
                Price = x.Price
            }).ToList(), assetPair, isBuy);
        }

        public static double CalcEffectivePrice(List<TradeQueueItem.TradeInfo> trades, AssetPair assetPair, bool isBuy)
        {
            return CalcEffectivePrice(trades.Select(x => new CommonTrade
            {
                Asset = x.MarketAsset,
                OppositeAsset = x.LimitAsset,
                Volume = x.MarketVolume,
                OppositeVolume = x.LimitVolume,
                Price = x.Price.GetValueOrDefault()
            }).ToList(), assetPair, isBuy);
        }

        private static double CalcEffectivePrice(IReadOnlyList<CommonTrade> trades, AssetPair assetPair, bool isBuy)
        {
            var usedTrades = trades.Where(x => Math.Abs(Math.Abs(x.Volume)) > LykkeConstants.Eps && Math.Abs(Math.Abs(x.OppositeVolume)) > LykkeConstants.Eps).ToList();

            var volume = usedTrades.Sum(x => x.Volume);
            var oppositeVolume = usedTrades.Sum(x => x.OppositeVolume);

            if (usedTrades.Count == 0)
                return trades[0].Price;

            if (usedTrades.Count == 1)
                return usedTrades[0].Price;

            if (assetPair.QuotingAssetId == trades[0].Asset)
                return (volume / oppositeVolume).TruncateDecimalPlaces(assetPair.Accuracy, isBuy);

            return (oppositeVolume / volume).TruncateDecimalPlaces(assetPair.Accuracy, isBuy);
        }



        class CommonTrade
        {
            public double Volume { get; set; }
            public string Asset { get; set; }
            public double OppositeVolume { get; set; }
            public string OppositeAsset { get; set; }
            public double Price { get; set; }
        }
    }
}
