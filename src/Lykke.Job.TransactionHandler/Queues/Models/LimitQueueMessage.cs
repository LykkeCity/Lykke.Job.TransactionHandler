using System;
using System.Collections.Generic;
using System.Linq;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Newtonsoft.Json;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Handlers;
using MessagePack;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class LimitQueueItem
    {        
        [JsonProperty("orders")]
        public List<LimitOrderWithTrades> Orders { get; set; }

        [MessagePackObject(keyAsPropertyName: true)]
        public class LimitOrderWithTrades
        {            
            [JsonProperty("order")]
            public LimitOrder Order { get; set; }
            
            [JsonProperty("trades")]
            public List<LimitTradeInfo> Trades { get; set; }
        }

        [MessagePackObject(keyAsPropertyName: true)]
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

        [MessagePackObject(keyAsPropertyName: true)]
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
            
            [CanBeNull]            
            [JsonProperty("feeInstruction")]
            public FeeInstruction FeeInstruction { get; set; }
            
            [CanBeNull]            
            [JsonProperty("feeTransfer")]
            public FeeTransfer FeeTransfer { get; set; }
        }
    }

    public static class LimitExt
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
