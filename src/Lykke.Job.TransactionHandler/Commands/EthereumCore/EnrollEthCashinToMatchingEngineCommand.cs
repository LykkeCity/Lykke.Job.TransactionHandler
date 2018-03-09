using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.TransactionHandler.Commands.EthereumCore
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class EnrollEthCashinToMatchingEngineCommand
    {
        public string TransactionHash { get; set; }
        public decimal Amount { get; set; }
        public string AssetId { get; set; }
        public string ClientAddress { get; set; }
        public string ClientId { get; set; }
        public bool CreatePendingActions { get; set; }
        public Guid CashinOperationId { get; set; }
    }
}
