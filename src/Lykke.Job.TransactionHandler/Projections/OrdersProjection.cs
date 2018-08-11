using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class OrdersProjection
    {
        private readonly IMarketOrdersRepository _marketOrdersRepository;

        public OrdersProjection(
            [NotNull] IMarketOrdersRepository marketOrdersRepository)
        {
            _marketOrdersRepository = marketOrdersRepository ?? throw new ArgumentNullException(nameof(marketOrdersRepository));
        }

        public async Task Handle(TradeCreatedEvent evt)
        {
            await _marketOrdersRepository.TryCreateAsync(evt.MarketOrder);

            ChaosKitty.Meow();
        }
    }
}