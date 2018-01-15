using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands.Bitcoin
{
    [ProtoContract]
    public class OffchainNotifyCommand
    {
        [ProtoMember(1)]
        public string ClientId { get; set; }
    }
}