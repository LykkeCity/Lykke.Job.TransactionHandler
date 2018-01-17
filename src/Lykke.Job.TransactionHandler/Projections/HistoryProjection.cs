using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.AzureRepositories;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class HistoryProjection
    {
        private readonly ILog _log;
        private readonly IMarketOrdersRepository _marketOrdersRepository;
        private readonly ITradeOperationsRepositoryClient _clientTradesRepository;

        public HistoryProjection(
            [NotNull] ILog log,
            [NotNull] IMarketOrdersRepository marketOrdersRepository,
            [NotNull] ITradeOperationsRepositoryClient clientTradesRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _marketOrdersRepository = marketOrdersRepository ?? throw new ArgumentNullException(nameof(marketOrdersRepository));
            _clientTradesRepository = clientTradesRepository ?? throw new ArgumentNullException(nameof(clientTradesRepository));
        }

        public async Task Handle(TradeCreatedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(HistoryProjection), nameof(TradeCreatedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            try
            {
                await _marketOrdersRepository.CreateAsync(evt.MarketOrder);
            }
            catch (Microsoft.WindowsAzure.Storage.StorageException exception)
            {
                if (exception.RequestInformation.HttpStatusCode != AzureHelper.ConflictStatusCode)
                    throw;
            }

            if (evt.ClientTrades != null)
            {
                await _clientTradesRepository.SaveAsync(evt.ClientTrades);
            }
        }
    }
}