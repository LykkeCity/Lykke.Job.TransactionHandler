using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.AzureRepositories;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class OrdersProjection
    {
        private readonly ILog _log;
        private readonly IMarketOrdersRepository _marketOrdersRepository;

        public OrdersProjection(
            [NotNull] ILog log,
            [NotNull] IMarketOrdersRepository marketOrdersRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _marketOrdersRepository = marketOrdersRepository ?? throw new ArgumentNullException(nameof(marketOrdersRepository));
        }

        public async Task Handle(TradeCreatedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OrdersProjection), nameof(TradeCreatedEvent), evt.ToJson(), "");

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
        }
    }
}