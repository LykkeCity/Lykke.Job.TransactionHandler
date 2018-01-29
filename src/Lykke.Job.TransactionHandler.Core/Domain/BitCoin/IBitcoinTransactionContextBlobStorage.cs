using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.BitCoin
{
    public interface IBitcoinTransactionContextBlobStorage
    {
        Task<string> Get(string transactionId);
        Task Set(string transactionId, string context);
    }
}
