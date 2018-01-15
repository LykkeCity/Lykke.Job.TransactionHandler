using Lykke.Service.Assets.Client.Models;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands.Ethereum
{
    [ProtoContract]
    public class EthGuaranteeTransferCommand
    {
        [ProtoMember(1)]
        public string OrderId { get; set; }
        [ProtoMember(2)]
        public string ClientId { get; set; }
        [ProtoMember(3)]
        public Asset Asset { get; set; }
        [ProtoMember(4)]
        public decimal Amount { get; set; }        
    }
}