using System;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Utils;

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

        public async Task<CommandHandlingResult> Handle(CreateOffchainCashoutRequestCommand command)
        {
            await _offchainRequestService.CreateOffchainRequestAndNotify(
                transactionId: command.Id,
                clientId: command.ClientId,
                assetId: command.AssetId,
                amount: command.Amount,
                orderId: null,
                type: OffchainTransferType.TrustedCashout);

            ChaosKitty.Meow();

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(CreateOffchainCashinRequestCommand command)
        {
            await _offchainRequestService.CreateOffchainRequestAndNotify(
                    transactionId: command.Id,
                    clientId: command.ClientId,
                    assetId: command.AssetId,
                    amount: command.Amount,
                    orderId: null,
                    type: OffchainTransferType.CashinToClient);

            ChaosKitty.Meow();

            return CommandHandlingResult.Ok();
        }
    }
}
