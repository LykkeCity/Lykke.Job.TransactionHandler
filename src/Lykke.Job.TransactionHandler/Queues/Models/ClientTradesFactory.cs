using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Service.Assets.Client;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using FeeType = Lykke.Service.OperationsRepository.AutorestClient.Models.FeeType;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    public class ClientTradesFactory : IClientTradesFactory
    {
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;

        public ClientTradesFactory(IAssetsServiceWithCache assetsServiceWithCache)
        {
            _assetsServiceWithCache = assetsServiceWithCache;
        }

        public async Task<ClientTrade[]> Create(TradeQueueItem.MarketOrder order, List<TradeQueueItem.TradeInfo> trades, string clientId)
        {
            var assetPair = await _assetsServiceWithCache.TryGetAssetPairAsync(order.AssetPairId);

            var trade = trades[0];
            var price = ConvertExtensions.CalcEffectivePrice(trades, assetPair, order.Volume > 0);
            var marketVolume = trades.Sum(x => x.MarketVolume);
            var limitVolume = trades.Sum(x => x.LimitVolume);

            var marketAssetRecord = CreateCommonPartForTradeRecord(trade, order.Id, price, order.AssetPairId);
            var limitAssetRecord = CreateCommonPartForTradeRecord(trade, order.Id, price, order.AssetPairId);

            marketAssetRecord.ClientId = limitAssetRecord.ClientId = clientId;

            marketAssetRecord.Amount = marketVolume;
            if (Math.Sign(marketVolume) == Math.Sign(limitVolume))
                marketAssetRecord.Amount *= -1;
            marketAssetRecord.AssetId = trade.MarketAsset;

            limitAssetRecord.Amount = limitVolume;
            limitAssetRecord.AssetId = trade.LimitAsset;

            foreach (var t in trades)
            {
                var transfer = t.Fees?.FirstOrDefault()?.Transfer;

                if (transfer != null)
                {
                    if (marketAssetRecord.AssetId == transfer.Asset)
                    {
                        marketAssetRecord.FeeSize += (double)transfer.Volume;
                        marketAssetRecord.FeeType = FeeType.Absolute;
                    }
                    else
                    {
                        limitAssetRecord.FeeSize += (double)transfer.Volume;
                        limitAssetRecord.FeeType = FeeType.Absolute;
                    }
                }
            }

            marketAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(marketAssetRecord.DateTime);
            limitAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(limitAssetRecord.DateTime);

            return new[] { marketAssetRecord, limitAssetRecord };
        }

        private static ClientTrade CreateCommonPartForTradeRecord(TradeQueueItem.TradeInfo trade, string marketOrderId, double price, string assetPairId)
        {
            return new ClientTrade
            {
                DateTime = trade.Timestamp,
                Price = price,
                LimitOrderId = trade.LimitOrderExternalId,
                MarketOrderId = marketOrderId,
                TransactionId = marketOrderId,
                AssetPairId = assetPairId,
                State = TransactionStates.SettledOffchain
            };
        }
    }
}
