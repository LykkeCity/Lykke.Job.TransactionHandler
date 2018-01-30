using System;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Services.Fee;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class FeeLogsProjection
    {
        private readonly ILog _log;
        private readonly IFeeLogService _feeLogService;

        public FeeLogsProjection(ILog log, IFeeLogService feeLogService)
        {
            _log = log;
            _feeLogService = feeLogService ?? throw new ArgumentNullException(nameof(feeLogService));
        }

        public async Task Handle(LimitOrderExecutedEvent evt)
        {
            if (evt.LimitOrder.Trades == null || evt.LimitOrder.Trades.Count == 0)
                return;

            await _feeLogService.WriteFeeInfo(evt.LimitOrder);

            _log.WriteInfo(nameof(FeeLogsProjection), nameof(Handle), $"Client {evt.LimitOrder.Order.ClientId}. Limit order {evt.LimitOrder.Order.Id}. Fee logs updated");

            ChaosKitty.Meow();
        }
    }
}