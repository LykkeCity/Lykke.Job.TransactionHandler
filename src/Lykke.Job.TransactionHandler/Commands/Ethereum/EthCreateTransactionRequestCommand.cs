using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands.Ethereum
{
    [ProtoContract]
    public class EthCreateTransactionRequestCommand
    {
        [ProtoMember(1)]
        public string Id { get; set; }
        [ProtoMember(2)]
        public string OrderId { get; set; }
        [ProtoMember(3)]
        public string ClientId { get; set; }       
        [ProtoMember(4)]
        public string AssetId { get; set; }
        [ProtoMember(5)]
        public decimal Amount { get; set; }
        [ProtoMember(6)]
        public string[] OperationIds { get; set; }        
    }
}