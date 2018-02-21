using System;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class CompleteOperationCommand
    {
        public Guid CommandId { get; set; }
    }
}
