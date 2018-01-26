using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Core.Contracts
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