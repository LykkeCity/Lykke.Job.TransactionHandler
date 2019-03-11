using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Handlers;
using Lykke.Service.Assets.Client.Models;

namespace Lykke.Job.TransactionHandler
{
    public static class ConvertExtensions
    {
        public static IReadOnlyList<ClientTrade> ToDomain(this LimitQueueItem.LimitOrderWithTrades item, AssetPair assetPair)
        {
            var trade = item.Trades[0];

            var limitVolume = item.Trades.Sum(x => x.Volume);
            var oppositeLimitVolume = item.Trades.Sum(x => x.OppositeVolume);

            var price = CalcEffectivePrice(item.Trades, assetPair, item.Order.Volume > 0);

            var clientId = trade.ClientId ?? item.Order.ClientId;

            var depositAssetRecord = CreateCommonPartForTradeRecord(trade, item.Order, item.Order.Id, price);
            var withdrawAssetRecord = CreateCommonPartForTradeRecord(trade, item.Order, item.Order.Id, price);

            depositAssetRecord.ClientId = withdrawAssetRecord.ClientId = clientId;

            depositAssetRecord.Amount = oppositeLimitVolume;
            depositAssetRecord.AssetId = trade.OppositeAsset;

            withdrawAssetRecord.Amount = -1 * limitVolume;
            withdrawAssetRecord.AssetId = trade.Asset;

            foreach (var t in item.Trades)
            {
                var transfer = t.Fees?.FirstOrDefault()?.Transfer;
                if (transfer != null)
                {
                    if (depositAssetRecord.AssetId == transfer.Asset)
                    {
                        depositAssetRecord.FeeSize += (double)transfer.Volume;
                        depositAssetRecord.FeeType =
                            Service.OperationsRepository.AutorestClient.Models.FeeType.Absolute;
                    }
                    else
                    {
                        withdrawAssetRecord.FeeSize += (double)transfer.Volume;
                        withdrawAssetRecord.FeeType =
                            Service.OperationsRepository.AutorestClient.Models.FeeType.Absolute;
                    }
                }
            }

            depositAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(depositAssetRecord.DateTime);
            withdrawAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(withdrawAssetRecord.DateTime);

            return new[] { depositAssetRecord, withdrawAssetRecord };
        }

        private static ClientTrade CreateCommonPartForTradeRecord(LimitQueueItem.LimitTradeInfo trade, IOrderBase limitOrder, string btcTransactionId, double price)
        {
            return new ClientTrade
            {
                DateTime = trade.Timestamp,
                Price = price,
                LimitOrderId = limitOrder.Id,
                OppositeLimitOrderId = trade.OppositeOrderExternalId,
                TransactionId = btcTransactionId,
                IsLimitOrderResult = true,
                State = Service.OperationsRepository.AutorestClient.Models.TransactionStates.SettledOffchain
            };
        }

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
                return Math.Abs((volume / oppositeVolume).TruncateDecimalPlaces(assetPair.Accuracy, isBuy));

            return Math.Abs((oppositeVolume / volume).TruncateDecimalPlaces(assetPair.Accuracy, isBuy));
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
