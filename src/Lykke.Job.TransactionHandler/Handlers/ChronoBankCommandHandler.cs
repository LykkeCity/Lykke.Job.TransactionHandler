using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Services.ChronoBank;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class ChronoBankCommandHandler
    {
        private readonly ILog _log;
        private readonly IChronoBankService _chronoBankService;

        public ChronoBankCommandHandler(
            [NotNull] ILog log,
            [NotNull] IChronoBankService chronoBankService)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _chronoBankService = chronoBankService ?? throw new ArgumentNullException(nameof(chronoBankService));
        }

        public async Task<CommandHandlingResult> Handle(Commands.ChronoBankCashOutCommand command)
        {
            await _log.WriteInfoAsync(nameof(SolarCoinCommandHandler), nameof(Commands.ChronoBankCashOutCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            await _chronoBankService.SendCashOutRequest(command.TransactionId, command.Address, command.Amount);

            return CommandHandlingResult.Ok();
        }
    }
}