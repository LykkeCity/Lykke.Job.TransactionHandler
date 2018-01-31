using System;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Core.Contracts
{
    public class SwapQueueItem
    {
        public string Id { get; set; }
        [JsonProperty("dateTime")]
        public DateTime Date { get; set; }

        public string ClientId1 { get; set; }
        [JsonProperty("asset1")]
        public string AssetId1 { get; set; }
        [JsonProperty("volume1")]
        public string Amount1 { get; set; }


        public string ClientId2 { get; set; }
        [JsonProperty("asset2")]
        public string AssetId2 { get; set; }
        [JsonProperty("volume2")]
        public string Amount2 { get; set; }
    }
}