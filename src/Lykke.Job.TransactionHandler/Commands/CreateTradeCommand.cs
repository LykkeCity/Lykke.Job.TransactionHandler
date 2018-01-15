using Lykke.Job.TransactionHandler.Queues.Models;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class CreateTradeCommand
    {
        [ProtoMember(1)]       
        public TradeQueueItem QueueMessage { get; set; }
    }
}