using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.JobTriggers.Triggers.Attributes;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

namespace Lykke.Job.TransactionHandler.TriggerHandlers
{
    public class HashEventsFunction
    {
        private readonly IBitCoinTransactionsRepository _bitcoinTransactionRepository;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;

        public HashEventsFunction(IBitCoinTransactionsRepository bitcoinTransactionRepository, IBitcoinTransactionService bitcoinTransactionService, ICashOperationsRepositoryClient cashOperationsRepositoryClient)
        {
            _bitcoinTransactionRepository = bitcoinTransactionRepository;
            _bitcoinTransactionService = bitcoinTransactionService;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
        }


        [QueueTrigger("hash-events", maxPollingIntervalMs: 100, maxDequeueCount: 1)]
        public async Task Process(HashEvent ev)
        {
            var tx = await _bitcoinTransactionRepository.FindByTransactionIdAsync(ev.Id);

            switch (tx?.CommandType)
            {
                case BitCoinCommands.CashOut:
                    var cashOutContext = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(tx.TransactionId);

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
