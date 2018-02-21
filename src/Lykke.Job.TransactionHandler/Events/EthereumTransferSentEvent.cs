using System;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class EthereumTransferSentEvent
    {
        public Guid TransferId { get; set; }
    }
}
