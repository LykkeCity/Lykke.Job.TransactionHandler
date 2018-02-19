using System;

namespace Lykke.Job.TransactionHandler.Core.Domain.BitCoin
{
    public enum CommandType
    {
        Unknown,
        Issue,
        CashOut,
        Transfer,
        TransferAll,
        Destroy,
        Swap
    }

    public class BaseCommand
    {
        public virtual CommandType Type { get; set; }
        public string Context { get; set; }
        public Guid? TransactionId { get; set; }
    }

    public class IssueCommand : BaseCommand
    {
        public string Multisig { get; set; }
        public string AssetId { get; set; }
        public decimal Amount { get; set; }

        public override CommandType Type => CommandType.Issue;
    }

    public class CashOutCommand : BaseCommand
    {
        public string SourceAddress { get; set; }
        public string DestinationAddress { get; set; }
        public string AssetId { get; set; }
        public decimal Amount { get; set; }

        public override CommandType Type => CommandType.CashOut;
    }

    public class TransferCommand : BaseCommand
    {
        public string SourceAddress { get; set; }
        public string DestinationAddress { get; set; }
        public string AssetId { get; set; }
        public decimal Amount { get; set; }

        public override CommandType Type => CommandType.Transfer;
    }
}