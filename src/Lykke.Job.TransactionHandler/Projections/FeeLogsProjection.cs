using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Domain.Fee;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Job.TransactionHandler.Handlers;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class FeeLogsProjection
    {
        private readonly ILog _log;
        private readonly IFeeLogRepository _feeLogRepository;

        public FeeLogsProjection(ILog log, IFeeLogRepository feeLogRepository)
        {
            _log = log;
            _feeLogRepository = feeLogRepository;
        }

        public async Task Handle(LimitOrderExecutedEvent evt)
        {
            if (evt.LimitOrder.Trades == null)
                return;

            var feeLogTasks = evt.LimitOrder.Trades.Select(ti =>
                _feeLogRepository.CreateAsync(new OrderFeeLog
                {
                    OrderId = evt.LimitOrder.Order.Id,
                    OrderStatus = evt.LimitOrder.Order.Status,
                    FeeTransfer = ti.FeeTransfer?.ToJson(),
                    FeeInstruction = ti.FeeInstruction?.ToJson(),
                    Type = "limit"
                }));

            await Task.WhenAll(feeLogTasks);
        }
    }
}