using System;
using Newtonsoft.Json;
using ProtoBuf;
using JetBrains.Annotations;
using System.Collections.Generic;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    [ProtoContract]
    public class CashInOutQueueMessage
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        [JsonProperty("dateTime")]
        public DateTime Date { get; set; }

        [ProtoMember(3)]
        public string ClientId { get; set; }

        [ProtoMember(4)]
        [JsonProperty("asset")]
        public string AssetId { get; set; }

        [ProtoMember(5)]
        [JsonProperty("volume")]
        public string Amount { get; set; }

        [ProtoMember(6)]
        [CanBeNull]
        [JsonProperty("feeInstructions")]
        public List<FeeInstruction> FeeInstructions { get; set; }

        [ProtoMember(7)]
        [CanBeNull]
        [JsonProperty("feeTransfers")]
        public List<FeeTransfer> FeeTransfers { get; set; }
    }
}