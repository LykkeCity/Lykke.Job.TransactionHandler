using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Newtonsoft.Json;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    [ProtoContract]
    public class TradeQueueItem
    {
        [ProtoMember(1)]
        [JsonProperty("order")]
        public MarketOrder Order { get; set; }

        [ProtoMember(2)]
        [JsonProperty("trades")]
        public List<TradeInfo> Trades { get; set; }

        [ProtoContract]
        public class MarketOrder : IMarketOrder
        {
            [ProtoMember(1)]
            [JsonProperty("externalId")]
            public string Id { get; set; }

            [ProtoMember(2)]
            [JsonProperty("id")]
            public string MatchingId { get; set; }

            [ProtoMember(3)]
            [JsonProperty("assetPairId")]
            public string AssetPairId { get; set; }

            [ProtoMember(4)]
            [JsonProperty("clientId")]
            public string ClientId { get; set; }

            [ProtoMember(5)]
            [JsonProperty("volume")]
            public double Volume { get; set; }

            [ProtoMember(6)]
            [JsonProperty("price")]
            public double? Price { get; set; }

            [ProtoMember(7)]
            [JsonProperty("status")]
            public string Status { get; set; }

            [ProtoMember(8)]
            [JsonProperty("createdAt")]
            public DateTime CreatedAt { get; set; }

            [ProtoMember(9)]
            [JsonProperty("registered")]
            public DateTime Registered { get; set; }

            [ProtoMember(10)]
            [JsonProperty("matchedAt")]
            public DateTime? MatchedAt { get; set; }

            [ProtoMember(11)]
            [JsonProperty("straight")]
            public bool Straight { get; set; }

            [ProtoMember(12)]
            [JsonProperty("reservedLimitVolume")]
            public double ReservedLimitVolume { get; set; }

            [ProtoMember(13)]
            [JsonProperty("dustSize")]
            public double? DustSize { get; set; }

            [ProtoMember(14)]
            double IOrderBase.Price
            {
                get { return Price.GetValueOrDefault(); }
                set { Price = value; }
            }
        }

        [ProtoContract]
        public class TradeInfo
        {
            [ProtoMember(1)]
            [JsonProperty("price")]
            public double? Price { get; set; }

            [ProtoMember(2)]
            [JsonProperty("limitOrderId")]
            public string LimitOrderId { get; set; }

            [ProtoMember(3)]
            [JsonProperty("limitOrderExternalId")]
            public string LimitOrderExternalId { get; set; }

            [ProtoMember(4)]
            [JsonProperty("timestamp")]
            public DateTime Timestamp { get; set; }

            [ProtoMember(5)]
            [JsonProperty("marketClientId")]
            public string MarketClientId { get; set; }

            [ProtoMember(6)]
            [JsonProperty("marketAsset")]
            public string MarketAsset { get; set; }

            [ProtoMember(7)]
            [JsonProperty("marketVolume")]
            public double MarketVolume { get; set; }

            [ProtoMember(8)]
            [JsonProperty("limitClientId")]
            public string LimitClientId { get; set; }

            [ProtoMember(9)]
            [JsonProperty("limitVolume")]
            public double LimitVolume { get; set; }

            [ProtoMember(10)]
            [JsonProperty("limitAsset")]
            public string LimitAsset { get; set; }

            [ProtoMember(11)]
            [CanBeNull]
            [JsonProperty("feeInstruction")]
            public FeeInstruction FeeInstruction { get; set; }

            [ProtoMember(12)]
            [CanBeNull]
            [JsonProperty("feeTransfer")]
            public FeeTransfer FeeTransfer { get; set; }
        }
    }
}