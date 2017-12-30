using Lykke.Job.TransactionHandler.Queues.Models;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class ManualUpdateCommand
    {
        [ProtoMember(1)]
        public string BlockchainHash { get; set; }
        [ProtoMember(2)]
        public string AddressTo { get; set; }

        [ProtoMember(3)]
        public CashInOutQueueMessage Message { get; set; }
    }
}