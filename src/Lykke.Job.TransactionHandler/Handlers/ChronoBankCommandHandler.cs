using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.ChronoBank;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class ChronoBankCommandHandler
    {
        private readonly ILog _log;
        private readonly IChronoBankCommandProducer _chronoBankCommandProducer;

        public ChronoBankCommandHandler(
            [NotNull] ILog log,
            [NotNull] IChronoBankCommandProducer chronoBankCommandProducer)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _chronoBankCommandProducer = chronoBankCommandProducer ?? throw new ArgumentNullException(nameof(chronoBankCommandProducer));
        }

        public async Task<CommandHandlingResult> Handle(Commands.ChronoBankCashOutCommand command)
        {
            await _log.WriteInfoAsync(nameof(SolarCoinCommandHandler), nameof(Commands.ChronoBankCashOutCommand), command.ToJson(), "");

            ChaosKitty.Meow();
            
            await _chronoBankCommandProducer.ProduceCashOutCommand(command.TransactionId, command.Address, command.Amount);

            return CommandHandlingResult.Ok();
        }
    }
}