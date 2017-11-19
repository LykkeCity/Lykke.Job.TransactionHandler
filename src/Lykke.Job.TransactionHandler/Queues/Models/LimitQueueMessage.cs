using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    public class LimitQueueItem
    {
        [JsonProperty("orders")]
        public List<LimitOrderWithTrades> Orders { get; set; }

        public class LimitOrderWithTrades
        {
            [JsonProperty("order")]
            public LimitOrder Order { get; set; }

            [JsonProperty("trades")]
            public List<LimitTradeInfo> Trades { get; set; }
        }

        public class LimitOrder : ILimitOrder
        {
            [JsonProperty("externalId")]
            public string Id { get; set; }

            [JsonProperty("id")]
            public string MatchingId { get; set; }

            [JsonProperty("assetPairId")]
            public string AssetPairId { get; set; }

            [JsonProperty("clientId")]
            public string ClientId { get; set; }

            [JsonProperty("volume")]
            public double Volume { get; set; }

            [JsonProperty("price")]
            public double Price { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("createdAt")]
            public DateTime CreatedAt { get; set; }

            [JsonProperty("registered")]
            public DateTime Registered { get; set; }

            [JsonProperty("remainingVolume")]
            public double RemainingVolume { get; set; }

            public bool Straight { get; set; } = true;
        }

        public class LimitTradeInfo
        {
            [JsonProperty("clientId")]
            public string ClientId { get; set; }

            [JsonProperty("asset")]
            public string Asset { get; set; }

            [JsonProperty("volume")]
            public double Volume { get; set; }

            [JsonProperty("price")]
            public double Price { get; set; }

            [JsonProperty("timestamp")]
            public DateTime Timestamp { get; set; }

            [JsonProperty("oppositeOrderId")]
            public string OppositeOrderId { get; set; }

            [JsonProperty("oppositeOrderExternalId")]
            public string OppositeOrderExternalId { get; set; }

            [JsonProperty("oppositeAsset")]
            public string OppositeAsset { get; set; }

            [JsonProperty("oppositeClientId")]
            public string OppositeClientId { get; set; }

            [JsonProperty("oppositeVolume")]
            public double OppositeVolume { get; set; }
        }
    }

    public static class LimitExt
    {
        public static IClientTrade[] ToDomainOffchain(this LimitQueueItem.LimitOrderWithTrades item, string btcTransactionId, IWalletCredentials walletCredentialsLimitA, IWalletCredentials walletCredentialsLimitB)
        {
            var trade = item.Trades[0];

            var limitVolume = item.Trades.Sum(x => x.Volume);
            var oppositeLimitVolume = item.Trades.Sum(x => x.OppositeVolume);

            var result = new List<IClientTrade>();

            result.AddRange(CreateTradeRecordForClientWithVolumes(trade, item.Order, btcTransactionId, walletCredentialsLimitA, walletCredentialsLimitB, limitVolume, oppositeLimitVolume));

            return result.ToArray();
        }

        private static IClientTrade[] CreateTradeRecordForClientWithVolumes(LimitQueueItem.LimitTradeInfo trade,
            ILimitOrder limitOrder,
            string btcTransactionId, IWalletCredentials walletCredentialsLimitA,
            IWalletCredentials walletCredentialsLimitB, double limitVolume, double oppositeLimitVolume)
        {
            var clientId = walletCredentialsLimitA?.ClientId ?? limitOrder.ClientId;

            var mutlisig = walletCredentialsLimitA?.MultiSig;
            var fromMultisig = walletCredentialsLimitB?.MultiSig;

            var depositAssetRecord = CreateCommonPartForTradeRecord(trade, limitOrder, btcTransactionId);
            var withdrawAssetRecord = CreateCommonPartForTradeRecord(trade, limitOrder, btcTransactionId);

            depositAssetRecord.ClientId = withdrawAssetRecord.ClientId = clientId;
            depositAssetRecord.AddressFrom = withdrawAssetRecord.AddressFrom = fromMultisig;
            depositAssetRecord.AddressTo = withdrawAssetRecord.AddressTo = mutlisig;
            depositAssetRecord.Multisig = withdrawAssetRecord.Multisig = mutlisig;

            depositAssetRecord.Amount = oppositeLimitVolume;
            depositAssetRecord.AssetId = trade.OppositeAsset;

            withdrawAssetRecord.Amount = -1 * limitVolume;
            withdrawAssetRecord.AssetId = trade.Asset;

            depositAssetRecord.Id = Utils.GenerateRecordId(depositAssetRecord.DateTime);
            withdrawAssetRecord.Id = Utils.GenerateRecordId(withdrawAssetRecord.DateTime);

            return new IClientTrade[] { depositAssetRecord, withdrawAssetRecord };
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
