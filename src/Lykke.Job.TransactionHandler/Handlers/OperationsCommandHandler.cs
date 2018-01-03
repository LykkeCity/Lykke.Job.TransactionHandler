using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class OperationsCommandHandler
    {
        private readonly ILog _log;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;

        public OperationsCommandHandler(
            [NotNull] ILog log,
            [NotNull] ICashOperationsRepositoryClient cashOperationsRepositoryClient)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient ?? throw new ArgumentNullException(nameof(cashOperationsRepositoryClient));
        }

        public async Task<CommandHandlingResult> Handle(Commands.RegisterCashInOutOperationCommand command)
        {
            await _log.WriteInfoAsync(nameof(OperationsCommandHandler), nameof(Commands.RegisterCashInOutOperationCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            var operation = command.Operation;
            var operationId = await _cashOperationsRepositoryClient.RegisterAsync(operation);
            if (operationId != operation.Id)
            {
                await _log.WriteWarningAsync(nameof(OperationsCommandHandler), nameof(Commands.RegisterCashInOutOperationCommand), operation.ToJson(),
                    $"Unexpected response from Operations Service: {operationId}");
            }

            return CommandHandlingResult.Ok();
        }
    }
}