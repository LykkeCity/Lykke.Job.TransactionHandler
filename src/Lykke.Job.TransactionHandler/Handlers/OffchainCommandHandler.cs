using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Commands.Bitcoin;
using Lykke.Job.TransactionHandler.Commands.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Events.Bitcoin;
using Lykke.Job.TransactionHandler.Sagas;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class OffchainCommandHandler
    {
        private readonly ILog _log;
        private readonly IOffchainRequestService _offchainRequestService;
        private readonly IOrdersRepository _ordersRepository;

        public OffchainCommandHandler(
            [NotNull] ILog log,
            [NotNull] IOffchainRequestService offchainRequestService,
            [NotNull] IOrdersRepository ordersRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _offchainRequestService = offchainRequestService ?? throw new ArgumentNullException(nameof(offchainRequestService));
            _ordersRepository = ordersRepository ?? throw new ArgumentNullException(nameof(ordersRepository));
        }

        public async Task<CommandHandlingResult> Handle(CreateOffchainCashoutRequestCommand command)
        {
            await _log.WriteInfoAsync(nameof(OffchainCommandHandler), nameof(CreateOffchainCashoutRequestCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            await _offchainRequestService.CreateOffchainRequestAndNotify(command.Id, command.ClientId, command.AssetId, command.Amount, null, OffchainTransferType.TrustedCashout);

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(TransferFromHubCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(OffchainCommandHandler), nameof(TransferFromHubCommand), command.ToJson());

            ChaosKitty.Meow();

            await _offchainRequestService.CreateOffchainRequest(
                command.TransactionId,
                command.ClientId,
                command.AssetId,
                command.Amount,
                command.OrderId,
                OffchainTransferType.FromHub);

            eventPublisher.PublishEvent(new OffchainRequestCreatedEvent { OrderId = command.OrderId, ClientId = command.ClientId });

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(ReturnCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(OffchainCommandHandler), nameof(ReturnCommand), command.ToJson());

            ChaosKitty.Meow();

            var order = await _ordersRepository.GetOrder(command.OrderId);
            var change = order.ReservedVolume - Math.Abs(command.Amount);

            if (change < 0)
                await _log.WriteWarningAsync(nameof(TradeSaga), nameof(EthBuyCommand),
                    $"Order: [{order.OrderId}], data: [{command.ToJson()}]",
                    "Diff is less than ZERO !");

            if (change > 0)
            {
                await _offchainRequestService.CreateOffchainRequest(
                    command.TransactionId,
                    command.ClientId,
                    command.AssetId,
                    command.Amount,
                    command.OrderId,
                    OffchainTransferType.FromHub);

                eventPublisher.PublishEvent(new OffchainRequestCreatedEvent { OrderId = command.OrderId, ClientId = command.ClientId });
            }

            return CommandHandlingResult.Ok();
        }
    }
}
