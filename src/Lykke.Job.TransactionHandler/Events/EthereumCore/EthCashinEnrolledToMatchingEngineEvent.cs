using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.TransactionHandler.Events.EthereumCore
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class EthCashinEnrolledToMatchingEngineEvent
    {
        public string TransactionHash { get; set; }
    }
}
