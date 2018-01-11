using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class CreateOffchainCashoutRequestCommand
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public string ClientId { get; set; }

        [ProtoMember(3)]
        public string AssetId { get; set; }

        [ProtoMember(4)]
        public decimal Amount { get; set; }
    }
}