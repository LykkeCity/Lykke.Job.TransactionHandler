using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class SaveIssueTransactionStateCommand
    {
        [ProtoMember(1)]
        public string TransactionId { get; set; }

        [ProtoMember(2)]
        public string RequestData { get; set; }

        [ProtoMember(3)]
        public IssueContextData Context { get; set; }
    }
}
