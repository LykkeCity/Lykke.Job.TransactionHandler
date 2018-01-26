using System;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Core.Contracts
{
    public class TransferQueueMessage
    {
        public string Id { get; set; }
        [JsonProperty("dateTime")]
        public DateTime Date { get; set; }

        public string FromClientId { get; set; }
        public string ToClientid { get; set; }

        [JsonProperty("asset")]
        public string AssetId { get; set; }
        [JsonProperty("volume")]
        public string Amount { get; set; }  

        [JsonProperty("feeInstruction")]
        public FeeSettings FeeSettings { get; set; }

        [JsonProperty("feeTransfer")]
        public FeeData FeeData { get; set; }
    }

    public class FeeSettings
    {
        public string Type { get; set; }
        public string SizeType { get; set; }
        public double Size { get; set; }
        public string SourceClientId { get; set; }
        public string TargetClientId { get; set; }
    }

    public class FeeData
    {
        public string ExternalId { get; set; }
        public string FromClientId { get; set; }
        public string ToClientid { get; set; }

        [JsonProperty("dateTime")]
        public DateTime Date { get; set; }

        [JsonProperty("volume")]
        public string Amount { get; set; }

        [JsonProperty("asset")]
        public string AassetId { get; set; }
    }
}
