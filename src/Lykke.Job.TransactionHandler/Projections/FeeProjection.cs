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

        public async Task Handle(TradeCreatedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(FeeProjection), nameof(TradeCreatedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var queueMessage = evt.QueueMessage;
            var feeLogTasks = queueMessage.Trades.Select(ti => _feeLogRepository.CreateAsync(new OrderFeeLog
            {
                OrderId = queueMessage.Order.Id,
                OrderStatus = queueMessage.Order.Status,
                FeeInstruction = ti.FeeInstruction?.ToJson(),
                FeeTransfer = ti.FeeTransfer?.ToJson(),
                Type = "market"
            }));
            await Task.WhenAll(feeLogTasks);
        }        
    }
}