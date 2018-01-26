using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ProtoBuf;

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

    [ProtoContract]
    public class FeeInstruction
    {
        [ProtoMember(1)]
        [JsonProperty("type")]
        public FeeType Type { get; set; }

        [ProtoMember(2)]
        [JsonProperty("takerSizeType")]
        public FeeSizeType SizeType { get; set; }

        [ProtoMember(3)]
        [JsonProperty("takerSize")]
        public double? Size { get; set; }

        [ProtoMember(4)]
        [JsonProperty("sourceClientId")]
        [CanBeNull]
        public string SourceClientId { get; set; }

        [ProtoMember(5)]
        [JsonProperty("targetClientId")]
        [CanBeNull]
        public string TargetClientId { get; set; }

        [ProtoMember(6)]
        [JsonProperty("assetIds")]
        public List<string> AssetIds { get; set; }
    }

    [ProtoContract]
    public class FeeTransfer
    {
        [ProtoMember(1)]
        [JsonProperty("externalId")]
        [CanBeNull]
        public string ExternalId { get; set; }

        [ProtoMember(2)]
        [JsonProperty("fromClientId")]
        public string FromClientId { get; set; }

        [ProtoMember(3)]
        [JsonProperty("toClientId")]
        public string ToClientId { get; set; }

        [ProtoMember(4)]
        [JsonProperty("dateTime")]
        public DateTime Date { get; set; }

        [ProtoMember(5)]
        [JsonProperty("volume")]
        public double Volume { get; set; }

        [ProtoMember(6)]
        [JsonProperty("asset")]
        public string Asset { get; set; }
    }

    [ProtoContract]
    public class Fee
    {
        [ProtoMember(1)]
        [JsonProperty("instruction")]
        public FeeInstruction Instruction { get; set; }

        [ProtoMember(2)]
        [JsonProperty("transfer")]
        [CanBeNull]
        public FeeTransfer Transfer { get; set; }
    }
}