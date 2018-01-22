using System;
using Newtonsoft.Json;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    [ProtoContract]
    public class TransferQueueMessage
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        [JsonProperty("dateTime")]
        public DateTime Date { get; set; }

        [ProtoMember(3)]
        public string FromClientId { get; set; }

        [ProtoMember(4)]
        public string ToClientid { get; set; }

        [ProtoMember(5)]
        [JsonProperty("asset")]
        public string AssetId { get; set; }

        [ProtoMember(6)]
        [JsonProperty("volume")]
        public string Amount { get; set; }

        [ProtoMember(7)]
        [JsonProperty("feeInstruction")]
        public FeeSettings FeeSettings { get; set; }

        [ProtoMember(8)]
        [JsonProperty("feeTransfer")]
        public FeeData FeeData { get; set; }
    }

    [ProtoContract]
    public class FeeSettings
    {
        [ProtoMember(1)]
        public string Type { get; set; }
        [ProtoMember(2)]
        public string SizeType { get; set; }
        [ProtoMember(3)]
        public double Size { get; set; }
        [ProtoMember(4)]
        public string SourceClientId { get; set; }
        [ProtoMember(5)]
        public string TargetClientId { get; set; }
    }

    [ProtoContract]
    public class FeeData
    {
        [ProtoMember(1)]
        public string ExternalId { get; set; }
        [ProtoMember(2)]
        public string FromClientId { get; set; }
        [ProtoMember(3)]
        public string ToClientid { get; set; }

        [ProtoMember(4)]
        [JsonProperty("dateTime")]
        public DateTime Date { get; set; }

        [ProtoMember(5)]
        [JsonProperty("volume")]
        public string Amount { get; set; }

        [ProtoMember(6)]
        [JsonProperty("asset")]
        public string AassetId { get; set; }
    }
}
