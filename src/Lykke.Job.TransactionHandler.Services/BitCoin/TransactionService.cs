using System.Threading.Tasks;
using Common;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;

namespace Lykke.Job.TransactionHandler.Services.BitCoin
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionsRepository _transactionsRepository;
        private readonly IBitcoinTransactionContextBlobStorage _contextBlobStorage;

        public TransactionService(ITransactionsRepository transactionsRepository, IBitcoinTransactionContextBlobStorage contextBlobStorage)
        {
            _transactionsRepository = transactionsRepository;
            _contextBlobStorage = contextBlobStorage;
        }

        public async Task<T> GetTransactionContext<T>(string transactionId) where T : BaseContextData
        {
            var fromBlob = await _contextBlobStorage.Get(transactionId);
            if (string.IsNullOrWhiteSpace(fromBlob))
            {
                var transaction = await _transactionsRepository.FindByTransactionIdAsync(transactionId);
                fromBlob = transaction?.ContextData;
            }

            if (fromBlob == null)
                return default(T);

            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(fromBlob);
        }

        public Task SetTransactionContext<T>(string transactionId, T context) where T : BaseContextData
        {
            return _contextBlobStorage.Set(transactionId, context.ToJson());
        }

        public Task CreateOrUpdateAsync(string meOrderId)
        {
            return _transactionsRepository.CreateOrUpdateAsync(meOrderId, BitCoinCommands.SwapOffchain);
        }
    }
}
