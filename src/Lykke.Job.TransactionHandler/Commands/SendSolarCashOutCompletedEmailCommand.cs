using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class SendSolarCashOutCompletedEmailCommand : ProcessCashOutBaseCommand
    {
        [ProtoMember(4)]
        public string ClientId { get; set; }
    }
}
