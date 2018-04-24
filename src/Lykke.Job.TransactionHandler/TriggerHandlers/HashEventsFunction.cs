using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.JobTriggers.Triggers.Attributes;
using Lykke.Service.Assets.Client;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

namespace Lykke.Job.TransactionHandler.TriggerHandlers
{
    public class HashEventsFunction
    {
        private readonly ITransactionsRepository _bitcoinTransactionRepository;
        private readonly ITransactionService _transactionService;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IClientAccountClient _сlientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly IAssetsServiceWithCache _assetsService;

        public HashEventsFunction(ITransactionsRepository bitcoinTransactionRepository, ITransactionService transactionService, ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            IClientAccountClient сlientAccountClient,
            ISrvEmailsFacade srvEmailsFacade,
            IAssetsServiceWithCache assetsService)
        {
            _bitcoinTransactionRepository = bitcoinTransactionRepository;
            _transactionService = transactionService;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
            _сlientAccountClient = сlientAccountClient;
            _srvEmailsFacade = srvEmailsFacade;
            _assetsService = assetsService;
        }


        [QueueTrigger("hash-events", maxPollingIntervalMs: 100, maxDequeueCount: 1)]
        public async Task Process(HashEvent ev)
        {
            var tx = await _bitcoinTransactionRepository.FindByTransactionIdAsync(ev.Id);
            string hash = ev.Hash;

            switch (tx?.CommandType)
            {
                case BitCoinCommands.CashOut:
                    var cashOutContext = await _transactionService.GetTransactionContext<CashOutContextData>(tx.TransactionId);
                    await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(cashOutContext.ClientId, cashOutContext.CashOperationId, hash);
                    var clientAccTask = _сlientAccountClient.GetByIdAsync(cashOutContext.ClientId);
                    var asset = await _assetsService.TryGetAssetAsync(cashOutContext.AssetId);
                    var clientAcc = await clientAccTask;

                    await _srvEmailsFacade.SendNoRefundOCashOutMail(clientAcc.PartnerId, clientAcc.Email, cashOutContext.Amount, asset.DisplayId, hash);

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
