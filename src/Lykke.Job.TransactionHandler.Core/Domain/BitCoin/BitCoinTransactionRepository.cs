using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.BitCoin
{
    public static class BitCoinCommands
    {
        public const string CashIn = "CashIn";
        public const string Swap = "Swap";
        public const string SwapOffchain = "SwapOffchain";
        public const string CashOut = "CashOut";
        public const string Transfer = "Transfer";
        public const string Destroy = "Destroy";
        public const string TransferAll = "TransferAll";
        public const string Issue = "Issue";
        public const string Refund = "Refund";
        public const string ManualUpdate = "ManualUpdate";
    }

    public interface IBitcoinTransaction
    {
        string TransactionId { get; }
        DateTime Created { get; }
        DateTime? ResponseDateTime { get; }
        string CommandType { get; }
        string RequestData { get; }
        string ResponseData { get; }
        string ContextData { get; }
        string BlockchainHash { get; }
    }

    public interface ITransactionsRepository
    {
        Task<bool> TryCreateAsync(string transactionId, string commandType, string requestData, string contextData, string response, string blockchainHash = null);
        Task CreateOrUpdateAsync(string transactionId, string commandType);
        Task<IBitcoinTransaction> FindByTransactionIdAsync(string transactionId);
        Task<IBitcoinTransaction> SaveResponseAndHashAsync(string transactionId, string resp, string hash, DateTime? dateTime = null);
        Task UpdateAsync(string transactionId, string requestData, string contextData, string response);
    }
}
