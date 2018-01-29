using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Domain.Fee;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Newtonsoft.Json;

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
            if (evt.LimitOrder.Trades == null || evt.LimitOrder.Trades.Count == 0)
                return;

            var feeLogs = evt.LimitOrder.Trades.Select(ti =>
                new OrderFeeLog
                {
                    OrderId = evt.LimitOrder.Order.Id,
                    OrderStatus = evt.LimitOrder.Order.Status,
                    FeeTransfer = ti.FeeTransfer?.ToJson(),
                    FeeInstruction = ti.FeeInstruction?.ToJson(),
                    Type = "limit"
                });

            var feeLogTasks = feeLogs.Select(fl => _feeLogRepository.CreateAsync(fl));

            await Task.WhenAll(feeLogTasks);

            _log.WriteInfo(nameof(FeeLogsProjection), JsonConvert.SerializeObject(feeLogs, Formatting.Indented), $"Client {evt.LimitOrder.Order.ClientId}. Limit order {evt.LimitOrder.Order.Id}. Fee logs updated");
        }
    }
}