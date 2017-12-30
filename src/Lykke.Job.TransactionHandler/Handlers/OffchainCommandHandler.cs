using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class OffchainCommandHandler
    {
        private readonly ILog _log;
        private readonly IOffchainRequestService _offchainRequestService;

        public OffchainCommandHandler(
            [NotNull] ILog log,
            [NotNull] IOffchainRequestService offchainRequestService)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _offchainRequestService = offchainRequestService ?? throw new ArgumentNullException(nameof(offchainRequestService));
        }

        public async Task<CommandHandlingResult> Handle(CreateOffchainCashoutRequestCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(OffchainCommandHandler), nameof(CreateOffchainCashoutRequestCommand), command.ToJson(), "");

            await _offchainRequestService.CreateOffchainRequestAndNotify(command.Id, command.ClientId, command.AssetId, command.Amount, null, OffchainTransferType.TrustedCashout);

            return CommandHandlingResult.Ok();
        }

    }
}
