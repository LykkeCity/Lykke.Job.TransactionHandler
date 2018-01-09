using System;

namespace Lykke.Job.TransactionHandler.Core.Domain.BitCoin
{
    #region Commands

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
        public double Amount { get; set; }

        public override CommandType Type => CommandType.Issue;
    }

    public class CashOutCommand : BaseCommand
    {
        public string SourceAddress { get; set; }
        public string DestinationAddress { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }

        public override CommandType Type => CommandType.CashOut;
    }

    public class TransferCommand : BaseCommand
    {
        public string SourceAddress { get; set; }
        public string DestinationAddress { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }

        public override CommandType Type => CommandType.Transfer;
    }

    public class TransferAllCommand : BaseCommand
    {
        public string SourceAddress { get; set; }
        public string DestinationAddress { get; set; }

        public override CommandType Type => CommandType.TransferAll;
    }

    public class DestroyCommand : BaseCommand
    {
        public string Address { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }

        public override CommandType Type => CommandType.Destroy;
    }

    public class SwapCommand : BaseCommand
    {
        public string MultisigCustomer1 { get; set; }
        public double Amount1 { get; set; }
        public string Asset1 { get; set; }

        public string MultisigCustomer2 { get; set; }
        public double Amount2 { get; set; }
        public string Asset2 { get; set; }

        public override CommandType Type => CommandType.Swap;
    }

    #endregion
}