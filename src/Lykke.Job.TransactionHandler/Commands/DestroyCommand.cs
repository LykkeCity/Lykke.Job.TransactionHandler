using Lykke.Job.TransactionHandler.Queues.Models;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class DestroyCommand
    {
        [ProtoMember(1)]
        public string TransactionId { get; set; }

        [ProtoMember(2)]
        public CashInOutQueueMessage Message { get; set; }

    }
}