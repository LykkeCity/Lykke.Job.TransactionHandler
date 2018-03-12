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
        public static IReadOnlyList<ClientTrade> ToDomainOffchain(this LimitQueueItem.LimitOrderWithTrades item, string btcTransactionId, string clientId, AssetPair assetPair)
        {
            var trade = item.Trades[0];

            var limitVolume = item.Trades.Sum(x => x.Volume);
            var oppositeLimitVolume = item.Trades.Sum(x => x.OppositeVolume);

            // if only one trade, we save price from this trade, otherwise we calculate effective price by trades
            var price = CalcEffectivePrice(item.Trades, assetPair, trade.Asset, limitVolume, oppositeLimitVolume);

            var result = new List<ClientTrade>();

            result.AddRange(CreateTradeRecordForClientWithVolumes(trade, item.Order, btcTransactionId, clientId, limitVolume, oppositeLimitVolume, price));

            return result;
        }

        private static IReadOnlyList<ClientTrade> CreateTradeRecordForClientWithVolumes(LimitQueueItem.LimitTradeInfo trade, ILimitOrder limitOrder, string btcTransactionId, string clientId, double limitVolume, double oppositeLimitVolume, double price)
        {
            clientId = clientId ?? limitOrder.ClientId;
            
            var depositAssetRecord = CreateCommonPartForTradeRecord(trade, limitOrder, btcTransactionId, price);
            var withdrawAssetRecord = CreateCommonPartForTradeRecord(trade, limitOrder, btcTransactionId, price);

            depositAssetRecord.ClientId = withdrawAssetRecord.ClientId = clientId;
            
            depositAssetRecord.Amount = oppositeLimitVolume;
            depositAssetRecord.AssetId = trade.OppositeAsset;

            withdrawAssetRecord.Amount = -1 * limitVolume;
            withdrawAssetRecord.AssetId = trade.Asset;
            
            var transfer = trade.Fees?.FirstOrDefault()?.Transfer;

            if (transfer != null)
            {
                if (depositAssetRecord.AssetId == transfer.Asset)
                {
                    depositAssetRecord.FeeSize = (double) transfer.Volume;
                    depositAssetRecord.FeeType = Service.OperationsRepository.AutorestClient.Models.FeeType.Absolute;
                }
                else
                {
                    withdrawAssetRecord.FeeSize = (double) transfer.Volume;
                    withdrawAssetRecord.FeeType = Service.OperationsRepository.AutorestClient.Models.FeeType.Absolute;
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
                IsLimitOrderResult = true
            };
        }

        private static double CalcEffectivePrice(List<LimitQueueItem.LimitTradeInfo> trades, AssetPair assetPair, string assetId, double volume, double oppositeVolume)
        {
            // if only one trade, or one of the volumes is equals to zero (after ME rounding)
            if (trades.Count == 1 || Math.Abs(volume) < LykkeConstants.Eps || Math.Abs(oppositeVolume) < LykkeConstants.Eps)
                return trades[0].Price;

            if (assetPair.QuotingAssetId == assetId)
                return (volume / oppositeVolume).TruncateDecimalPlaces(assetPair.Accuracy, true);

            return (oppositeVolume / volume).TruncateDecimalPlaces(assetPair.Accuracy, true);
        }
    }
}
