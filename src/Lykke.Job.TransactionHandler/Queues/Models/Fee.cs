using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using MessagePack;
using Newtonsoft.Json.Converters;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Queues.Models
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
        
        [JsonProperty("sizeType")]
        public FeeSizeType SizeType { get; set; }
        
        [JsonProperty("size")]
        public double? Size { get; set; }
        
        [JsonProperty("sourceClientId")]
        [CanBeNull]
        public string SourceClientId { get; set; }
        
        [JsonProperty("targetClientId")]
        [CanBeNull]
        public string TargetClientId { get; set; }
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
        public double Volume { get; set; }
        
        [JsonProperty("asset")]
        public string Asset { get; set; }
    }
}