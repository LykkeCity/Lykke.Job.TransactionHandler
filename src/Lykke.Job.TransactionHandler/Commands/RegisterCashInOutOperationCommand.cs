using Lykke.Service.OperationsRepository.AutorestClient.Models;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class RegisterCashInOutOperationCommand
    {
        [ProtoMember(1)]
        public CashInOutOperation Operation { get; set; }
    }
}
