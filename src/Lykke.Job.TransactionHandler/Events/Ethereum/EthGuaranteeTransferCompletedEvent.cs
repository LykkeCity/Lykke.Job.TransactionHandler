using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Events.Ethereum
{
    [ProtoContract]
    public class EthGuaranteeTransferCompletedEvent
    {
        [ProtoMember(1)]
        public string OrderId { get; set; }
    }
}