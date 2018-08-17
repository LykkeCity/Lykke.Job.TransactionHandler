using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.JobTriggers.Triggers.Attributes;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using Lykke.Service.PersonalData.Contract;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.TriggerHandlers
{
    public class HashEventsFunction
    {
        private readonly ITransactionsRepository _bitcoinTransactionRepository;
        private readonly ITransactionService _transactionService;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IClientAccountClient _сlientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly IPersonalDataService _personalDataService;

        public HashEventsFunction(ITransactionsRepository bitcoinTransactionRepository, ITransactionService transactionService, ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            IClientAccountClient сlientAccountClient,
            ISrvEmailsFacade srvEmailsFacade,
            IPersonalDataService personalDataService)
        {
            _bitcoinTransactionRepository = bitcoinTransactionRepository;
            _transactionService = transactionService;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
            _сlientAccountClient = сlientAccountClient;
            _srvEmailsFacade = srvEmailsFacade;
            _personalDataService = personalDataService;
        }


        [QueueTrigger("hash-events", maxPollingIntervalMs: 100, maxDequeueCount: 1)]
        public async Task Process(HashEvent ev)
        {
            var tx = await _bitcoinTransactionRepository.FindByTransactionIdAsync(ev.Id);

            if (tx == null)
                return;

            string hash = ev.Hash;

            switch (tx.CommandType)
            {
                case BitCoinCommands.CashOut:
                    var cashOutContext = await _transactionService.GetTransactionContext<CashOutContextData>(tx.TransactionId);
                    var clientAcc = await _сlientAccountClient.GetByIdAsync(cashOutContext.ClientId);
                    var clientEmail = await _personalDataService.GetEmailAsync(cashOutContext.ClientId);

                    await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(cashOutContext.ClientId, cashOutContext.CashOperationId, hash);
                    await _srvEmailsFacade.SendNoRefundOCashOutMail(clientAcc.PartnerId, clientEmail, cashOutContext.Amount, cashOutContext.AssetId, hash);

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
