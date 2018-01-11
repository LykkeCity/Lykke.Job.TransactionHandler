using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class SolarCoinCommandHandler
    {
        private readonly ILog _log;
        private readonly ISrvSolarCoinHelper _srvSolarCoinHelper;

        public SolarCoinCommandHandler(
            [NotNull] ILog log,
            [NotNull] ISrvSolarCoinHelper srvSolarCoinHelper)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _srvSolarCoinHelper = srvSolarCoinHelper ?? throw new ArgumentNullException(nameof(srvSolarCoinHelper));
        }

        public async Task<CommandHandlingResult> Handle(Commands.SolarCashOutCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(SolarCoinCommandHandler), nameof(Commands.SolarCashOutCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            var slrAddress = new SolarCoinAddress(command.Address);

            await _srvSolarCoinHelper.SendCashOutRequest(command.TransactionId, slrAddress, command.Amount);

            eventPublisher.PublishEvent(new SolarCashOutCompletedEvent
            {
                ClientId = command.ClientId,
                Address = command.Address,
                Amount = command.Amount
            });

            return CommandHandlingResult.Ok();
        }

    }
}