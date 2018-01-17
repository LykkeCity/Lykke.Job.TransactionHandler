using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.CashOperations
{
    public interface IForwardWithdrawal
    {
        string Id { get; }
        string AssetId { get; }
        string ClientId { get; }
        double Amount { get; }
        DateTime DateTime { get; }

        string CashInId { get; }
    }

    public interface IForwardWithdrawalRepository
    {
        Task SetLinkedCashInOperationId(string clientId, string id, string cashInId);
    }
}