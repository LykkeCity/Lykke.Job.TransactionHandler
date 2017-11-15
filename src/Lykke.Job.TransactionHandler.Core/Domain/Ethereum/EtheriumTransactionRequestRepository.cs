using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Ethereum
{
    public enum OperationType
    {
        CashOut,
        Trade,
        TransferToTrusted,
        TransferFromTrusted,
        TransferBetweenTrusted
    }

    public interface IEthereumTransactionRequest
    {
        Guid Id { get; set; }
        string ClientId { get; set; }
        string Hash { get; set; }
        string AssetId { get; set; }
        decimal Volume { get; set; }
        string AddressTo { get; set; }

        Transaction SignedTransfer { get; set; }
        string OrderId { get; set; }

        OperationType OperationType { get; set; }
        string[] OperationIds { get; set; }
    }

    public class Transaction
    {
        public Guid Id { get; set; }
        public string Sign { get; set; }
    }

    public class EthereumTransactionRequest : IEthereumTransactionRequest
    {
        public Guid Id { get; set; }
        public string ClientId { get; set; }
        public string Hash { get; set; }
        public string AssetId { get; set; }
        public decimal Volume { get; set; }
        public string AddressTo { get; set; }

        public Transaction SignedTransfer { get; set; }
        public string OrderId { get; set; }

        public OperationType OperationType { get; set; }
        public string[] OperationIds { get; set; }
    }

    public interface IEthereumTransactionRequestRepository
    {
        Task<IEthereumTransactionRequest> GetAsync(Guid id);
        Task<IEthereumTransactionRequest> GetByOrderAsync(string orderId);
        Task InsertAsync(IEthereumTransactionRequest request, bool insertOrder = true);
        Task UpdateAsync(IEthereumTransactionRequest request);
    }
}