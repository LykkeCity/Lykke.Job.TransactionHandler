using System;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class TransferEthereumCommand
    {
        public Guid TransactionId { get; set; }
    }
}
