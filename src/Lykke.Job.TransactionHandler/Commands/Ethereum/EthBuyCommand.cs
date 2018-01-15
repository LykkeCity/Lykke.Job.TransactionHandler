using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands.Ethereum
{
    [ProtoContract]
    public class EthBuyCommand
    {
        [ProtoMember(1)]
        public string TransactionId { get; set; }
        [ProtoMember(2)]
        public string OrderId { get; set; }
        [ProtoMember(3)]
        public string ClientId { get; set; }
        [ProtoMember(4)]
        public string AssetId { get; set; }
        [ProtoMember(5)]
        public decimal Amount { get; set; }        
    }
}