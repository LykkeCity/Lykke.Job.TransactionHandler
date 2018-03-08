using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.TransactionHandler.Events.EthereumCore
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class StartCashinEvent
    {
        public string ClientId { get; set; }
        public string ClientAddress { get; set; }
        public string AssetId { get; set; }
        public decimal Amount { get; set; }
        public string Hash { get; set; }
        public bool CreatePendingActions { get; set; }
    }
}
