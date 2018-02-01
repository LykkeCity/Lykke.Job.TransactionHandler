using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Core.Contracts
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class CashInOutQueueMessage
    {
        public string Id { get; set; }
        
        [JsonProperty("dateTime")]
        public DateTime Date { get; set; }
        
        public string ClientId { get; set; }
        
        [JsonProperty("asset")]
        public string AssetId { get; set; }
        
        [JsonProperty("volume")]
        public string Amount { get; set; }

        [JsonProperty("fees")]
        [CanBeNull]
        public List<Fee> Fees { get; set; }
    }
}