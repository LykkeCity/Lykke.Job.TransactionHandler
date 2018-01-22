using System;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Domain.Fee;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class FeeProjection
    {
        private readonly ILog _log;
        private readonly IFeeLogRepository _feeLogRepository;

        public FeeProjection(
            [NotNull] ILog log,
            [NotNull] IFeeLogRepository feeLogRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _feeLogRepository = feeLogRepository ?? throw new ArgumentNullException(nameof(feeLogRepository));
        }

        // todo: remove?
        public async Task Handle(TradeCreatedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(FeeProjection), nameof(TradeCreatedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.QueueMessage;
            // todo: multithread + idempotent
            var feeLogTasks = message.Trades.Select(ti => _feeLogRepository.CreateAsync(new OrderFeeLog
            {
                OrderId = message.Order.Id,
                OrderStatus = message.Order.Status,
                FeeInstruction = ti.FeeInstruction?.ToJson(),
                FeeTransfer = ti.FeeTransfer?.ToJson(),
                Type = "market"
            }));
            await Task.WhenAll(feeLogTasks);
        }
    }
}