using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Domain.Logs;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class TransfersProjection
    {
        private readonly ILog _log;
        private readonly ITransferLogRepository _transferLogRepository;

        public TransfersProjection(
            [NotNull] ILog log,
            [NotNull] ITransferLogRepository transferLogRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _transferLogRepository = transferLogRepository ?? throw new ArgumentNullException(nameof(transferLogRepository));
        }

        public async Task Handle(TransferOperationStateSavedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(TransfersProjection), nameof(TransferOperationStateSavedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.QueueMessage;
            await _transferLogRepository.CreateAsync(message.Id, message.Date, message.FromClientId, message.ToClientid,
                message.AssetId, message.Amount, message.FeeSettings?.ToJson(), message.FeeData?.ToJson());
        }
    }
}