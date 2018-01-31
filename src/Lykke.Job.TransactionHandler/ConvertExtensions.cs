using System.Collections.Generic;
using System.Linq;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Handlers;

namespace Lykke.Job.TransactionHandler
{
    public static class ConvertExtensions
    {
        public static ClientTrade[] ToDomainOffchain(this LimitQueueItem.LimitOrderWithTrades item, string btcTransactionId, string clientId)
        {
            var trade = item.Trades[0];

            var limitVolume = item.Trades.Sum(x => x.Volume);
            var oppositeLimitVolume = item.Trades.Sum(x => x.OppositeVolume);

            var result = new List<ClientTrade>();

            result.AddRange(CreateTradeRecordForClientWithVolumes(trade, item.Order, btcTransactionId, clientId, limitVolume, oppositeLimitVolume));

            return result.ToArray();
        }

        private static ClientTrade[] CreateTradeRecordForClientWithVolumes(LimitQueueItem.LimitTradeInfo trade, ILimitOrder limitOrder, string btcTransactionId, string clientId, double limitVolume, double oppositeLimitVolume)
        {
            clientId = clientId ?? limitOrder.ClientId;
            
            var depositAssetRecord = CreateCommonPartForTradeRecord(trade, limitOrder, btcTransactionId);
            var withdrawAssetRecord = CreateCommonPartForTradeRecord(trade, limitOrder, btcTransactionId);

            depositAssetRecord.ClientId = withdrawAssetRecord.ClientId = clientId;
            
            depositAssetRecord.Amount = oppositeLimitVolume;
            depositAssetRecord.AssetId = trade.OppositeAsset;

            withdrawAssetRecord.Amount = -1 * limitVolume;
            withdrawAssetRecord.AssetId = trade.Asset;

            depositAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(depositAssetRecord.DateTime);
            withdrawAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(withdrawAssetRecord.DateTime);

            return new[] { depositAssetRecord, withdrawAssetRecord };
        }

        private static ClientTrade CreateCommonPartForTradeRecord(LimitQueueItem.LimitTradeInfo trade, ILimitOrder limitOrder,
            string btcTransactionId)
        {
            return new ClientTrade
            {
                DateTime = trade.Timestamp,
                Price = trade.Price,
                LimitOrderId = limitOrder.Id,
                OppositeLimitOrderId = trade.OppositeOrderExternalId,
                TransactionId = btcTransactionId,
                IsLimitOrderResult = true
            };
        }
    }
}