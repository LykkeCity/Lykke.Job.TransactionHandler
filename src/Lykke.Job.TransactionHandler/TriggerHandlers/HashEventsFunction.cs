using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.JobTriggers.Triggers.Attributes;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

namespace Lykke.Job.TransactionHandler.TriggerHandlers
{
    public class HashEventsFunction
    {
        private readonly ITransactionsRepository _bitcoinTransactionRepository;
        private readonly ITransactionService _transactionService;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;

        public HashEventsFunction(ITransactionsRepository bitcoinTransactionRepository, ITransactionService transactionService, ICashOperationsRepositoryClient cashOperationsRepositoryClient)
        {
            _bitcoinTransactionRepository = bitcoinTransactionRepository;
            _transactionService = transactionService;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
        }


        [QueueTrigger("hash-events", maxPollingIntervalMs: 100, maxDequeueCount: 1)]
        public async Task Process(HashEvent ev)
        {
            var tx = await _bitcoinTransactionRepository.FindByTransactionIdAsync(ev.Id);

            switch (tx?.CommandType)
            {
                case BitCoinCommands.CashOut:
                    var cashOutContext = await _transactionService.GetTransactionContext<CashOutContextData>(tx.TransactionId);

                    await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(cashOutContext.ClientId, cashOutContext.CashOperationId, ev.Hash);

                    break;
            }
        }
    }

    public class HashEvent
    {
        public string Id { get; set; }
        public string Hash { get; set; }
    }
}
