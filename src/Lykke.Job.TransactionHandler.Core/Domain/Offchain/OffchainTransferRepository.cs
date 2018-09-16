using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Offchain
{
    public enum OffchainTransferType
    {
        None = 0,
        FromClient = 1,
        FromHub = 2,
        CashinFromClient = 3,
        ClientCashout = 4,
        FullCashout = 5, // not used
        CashinToClient = 6,
        OffchainCashout = 7,
        HubCashout = 8,
        DirectTransferFromClient = 9,
        TrustedCashout = 10
    }

    public interface IOffchainTransfer
    {
        string Id { get; }
        string ClientId { get; }
        string AssetId { get; }
        decimal Amount { get; }
        bool Completed { get; }
        string OrderId { get; }
        DateTime CreatedDt { get; }
        string ExternalTransferId { get; }
        OffchainTransferType Type { get; }
        bool ChannelClosing { get; }
        bool Onchain { get; }
        bool IsChild { get; }
        string ParentTransferId { get; }
        string AdditionalDataJson { get; set; }
        string BlockchainHash { get; set; }
    }

    public interface IOffchainTransferRepository
    {
        Task<IOffchainTransfer> CreateTransfer(string transactionId, string clientId, string assetId, decimal amount, OffchainTransferType type, string externalTransferId, string orderId, bool channelClosing = false);
    }
}