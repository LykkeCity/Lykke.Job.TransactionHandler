using Lykke.Job.TransactionHandler.Core.Contracts;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class RegisterCashInOutOperationCommand
    {
        [ProtoMember(1)]
        public CashInOutQueueMessage Message { get; set; }
    }
}
