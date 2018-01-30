using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MessagePack;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Core.Contracts
{
    [MessagePackObject(keyAsPropertyName: true)]
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

        [CanBeNull]
        [JsonProperty("fees")]
        public List<Fee> Fees { get; set; }
    }
}
