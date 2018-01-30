using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Services.Fee;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class FeeProjection
    {
        private readonly ILog _log;
        private readonly IFeeLogService _feeLogService;

        public FeeProjection(
            [NotNull] ILog log,
            [NotNull] IFeeLogService feeLogService)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _feeLogService = feeLogService ?? throw new ArgumentNullException(nameof(feeLogService));
        }

        // todo: remove?
        public async Task Handle(TradeCreatedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(FeeProjection), nameof(TradeCreatedEvent), evt.ToJson(), "");

            await _feeLogService.WriteFeeInfo(evt.QueueMessage);

            ChaosKitty.Meow();
        }
    }
}