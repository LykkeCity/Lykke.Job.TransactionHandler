using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.TransactionHandler.Events.EthereumCore
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class CashinDetectedEvent
    {
        public string ClientId { get; set; }
        public string ClientAddress { get; set; }
        public string AssetId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionHash { get; set; }
        public bool CreatePendingActions { get; set; }
    }
}
