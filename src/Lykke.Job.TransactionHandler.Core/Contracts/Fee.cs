using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Lykke.Job.TransactionHandler.Core.Contracts
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum FeeType
    {
        NO_FEE = 0,
        CLIENT_FEE,
        EXTERNAL_FEE
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum FeeSizeType
    {
        PERCENTAGE = 0,
        ABSOLUTE
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class FeeInstruction
    {
        [JsonProperty("type")]
        public FeeType Type { get; set; }

        [JsonProperty("takerSizeType")]
        public FeeSizeType SizeType { get; set; }

        [JsonProperty("takerSize")]
        public decimal? Size { get; set; }

        [JsonProperty("sourceClientId")]
        [CanBeNull]
        public string SourceClientId { get; set; }

        [JsonProperty("targetClientId")]
        [CanBeNull]
        public string TargetClientId { get; set; }

        [JsonProperty("assetIds")]
        public List<string> AssetIds { get; set; }
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class FeeTransfer
    {
        [JsonProperty("externalId")]
        [CanBeNull]
        public string ExternalId { get; set; }

        [JsonProperty("fromClientId")]
        public string FromClientId { get; set; }

        [JsonProperty("toClientId")]
        public string ToClientId { get; set; }

        [JsonProperty("dateTime")]
        public DateTime Date { get; set; }

        [JsonProperty("volume")]
        public decimal Volume { get; set; }

        [JsonProperty("asset")]
        public string Asset { get; set; }
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class Fee
    {
        [JsonProperty("instruction")]
        public FeeInstruction Instruction { get; set; }

        [JsonProperty("transfer")]
        [CanBeNull]
        public FeeTransfer Transfer { get; set; }
    }
}