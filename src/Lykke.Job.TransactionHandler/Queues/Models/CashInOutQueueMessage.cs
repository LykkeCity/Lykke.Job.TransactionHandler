using System;
using Newtonsoft.Json;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Queues.Models
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
    }
}