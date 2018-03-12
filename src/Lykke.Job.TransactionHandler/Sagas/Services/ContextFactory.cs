using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Service.Assets.Client;
using Lykke.Service.ClientAccount.Client;

namespace Lykke.Job.TransactionHandler.Sagas.Services
{
    public enum OperationStatus
    {
        Matched
    }

    public class ContextFactory : IContextFactory
    {
        private readonly IClientTradesFactory _clientTradesFactory;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;

        public ContextFactory(
            IClientTradesFactory clientTradesFactory,
            IClientAccountClient clientAccountClient,
            IAssetsServiceWithCache assetsServiceWithCache)
        {
            _clientTradesFactory = clientTradesFactory;
            _clientAccountClient = clientAccountClient;
            _assetsServiceWithCache = assetsServiceWithCache;
        }

        public async Task<SwapOffchainContextData> FillTradeContext(SwapOffchainContextData context, TradeQueueItem.MarketOrder order, List<TradeQueueItem.TradeInfo> trades, string clientId)
        {
            var marketVolume = trades.Sum(x => x.MarketVolume);
            var limitVolume = trades.Sum(x => x.LimitVolume);

            // if only one trade, we save price from this trade, otherwise we calculate effective price by trades
            var price = await CalcEffectivePrice(trades, order.AssetPairId, trades[0].MarketAsset, marketVolume, limitVolume);

            context.Order = order;
            context.Trades = trades;
            context.ClientTrades = await _clientTradesFactory.Create(order.Id, clientId, order.AssetPairId, trades[0], marketVolume, limitVolume, price);
            context.IsTrustedClient = (await _clientAccountClient.IsTrustedAsync(clientId)).Value;
            context.Status = OperationStatus.Matched;

            if (!context.IsTrustedClient)
            {
                var aggregatedTrades = await AggregateSwaps(trades);

                context.SellTransfer = aggregatedTrades.sellTransfer;
                context.BuyTransfer = aggregatedTrades.buyTransfer;

                var operations = new[] { aggregatedTrades.sellTransfer, aggregatedTrades.buyTransfer };

                foreach (var operation in operations)
                {
                    var trade = context.ClientTrades.FirstOrDefault(x =>
                        x.ClientId == clientId && x.AssetId == operation.Asset.Id &&
                        Math.Abs(x.Amount - (double)operation.Amount) < LykkeConstants.Eps);

                    // find existed operation (which was inserted in LW after guarantee transfer)
                    var existed = context.Operations.FirstOrDefault(x => x.ClientId == clientId && x.AssetId == operation.Asset.Id);

                    if (existed != null)
                    {
                        existed.ClientTradeId = trade?.Id;
                        continue;
                    }

                    context.Operations.Add(new SwapOffchainContextData.Operation()
                    {
                        TransactionId = operation.TransferId,
                        Amount = operation.Amount,
                        ClientId = clientId,
                        AssetId = operation.Asset.Id,
                        ClientTradeId = trade?.Id
                    });
                }
            }
            return context;
        }

        private async Task<(AggregatedTransfer sellTransfer, AggregatedTransfer buyTransfer)> AggregateSwaps(List<TradeQueueItem.TradeInfo> swaps)
        {
            var marketAsset = swaps[0].MarketAsset;
            var limitAsset = swaps[0].LimitAsset;

            var sellTransfer = new AggregatedTransfer
            {
                TransferId = Guid.NewGuid().ToString(),
                Asset = await _assetsServiceWithCache.TryGetAssetAsync(marketAsset),
                Amount = -swaps.Sum(x => Convert.ToDecimal(x.MarketVolume))
            };

            var buyTransfer = new AggregatedTransfer
            {
                TransferId = Guid.NewGuid().ToString(),
                Asset = await _assetsServiceWithCache.TryGetAssetAsync(limitAsset),
                Amount = swaps.Sum(x => Convert.ToDecimal(x.LimitVolume))
            };

            return (sellTransfer, buyTransfer);
        }

        private async Task<double> CalcEffectivePrice(List<TradeQueueItem.TradeInfo> trades, string assetPair, string assetId, double volume, double oppositeVolume)
        {
            // if only one trade, or one of the volumes is equals to zero (after ME rounding)
            if (trades.Count == 1 || Math.Abs(volume) < LykkeConstants.Eps || Math.Abs(oppositeVolume) < LykkeConstants.Eps)
                return trades[0].Price.GetValueOrDefault();

            var pair = await _assetsServiceWithCache.TryGetAssetPairAsync(assetPair);

            if (pair.QuotingAssetId == assetId)
                return (volume / oppositeVolume).TruncateDecimalPlaces(pair.Accuracy, true);

            return (oppositeVolume / volume).TruncateDecimalPlaces(pair.Accuracy, true);
        }
    }
}