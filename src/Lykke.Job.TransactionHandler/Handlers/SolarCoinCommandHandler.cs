using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.SolarCoin;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class SolarCoinCommandHandler
    {
        private readonly ILog _log;
        private readonly ISrvSolarCoinCommandProducer _solarCoinCommandProducer;

        public SolarCoinCommandHandler(
            [NotNull] ILog log,
            [NotNull] ISrvSolarCoinCommandProducer solarCoinCommandProducer)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _solarCoinCommandProducer = solarCoinCommandProducer ?? throw new ArgumentNullException(nameof(solarCoinCommandProducer));
        }

        public async Task<CommandHandlingResult> Handle(Commands.SolarCashOutCommand command, IEventPublisher eventPublisher)
        {
            var slrAddress = new SolarCoinAddress(command.Address);
            
            await _solarCoinCommandProducer.ProduceCashOutCommand(command.TransactionId, slrAddress, command.Amount);

            ChaosKitty.Meow();

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