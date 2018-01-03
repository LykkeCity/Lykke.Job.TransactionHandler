using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class CashInCommandHandler
    {
        private readonly ILog _log;
        private readonly IForwardWithdrawalRepository _forwardWithdrawalRepository;

        public CashInCommandHandler(
            [NotNull] ILog log,
            [NotNull] IForwardWithdrawalRepository forwardWithdrawalRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _forwardWithdrawalRepository = forwardWithdrawalRepository ?? throw new ArgumentNullException(nameof(forwardWithdrawalRepository));
        }

        public async Task<CommandHandlingResult> Handle(Commands.SetLinkedCashInOperationCommand command)
        {
            await _log.WriteInfoAsync(nameof(SolarCoinCommandHandler), nameof(Commands.SetLinkedCashInOperationCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            await _forwardWithdrawalRepository.SetLinkedCashInOperationId(command.ClientId, command.Id, command.CashInId);

            return CommandHandlingResult.Ok();
        }
    }
}