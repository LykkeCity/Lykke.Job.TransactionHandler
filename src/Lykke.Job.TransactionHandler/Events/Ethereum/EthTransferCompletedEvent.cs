using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events.Ethereum
{
    [ProtoContract]
    public class EthTransferCompletedEvent
    {
        [ProtoMember(1)]
        public string OrderId { get; set; }
    }
}