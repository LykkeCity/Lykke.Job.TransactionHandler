using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class CreateTransactionCommand
    {
        [ProtoMember(1)]
        public string OrderId { get; set; }
    }
}