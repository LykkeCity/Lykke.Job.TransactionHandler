using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class ForwardWithdrawalCommandHandler
    {
        private readonly ILog _log;
        private readonly IForwardWithdrawalRepository _forwardWithdrawalRepository;

        public ForwardWithdrawalCommandHandler(
            [NotNull] ILog log,
            [NotNull] IForwardWithdrawalRepository forwardWithdrawalRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _forwardWithdrawalRepository = forwardWithdrawalRepository ?? throw new ArgumentNullException(nameof(forwardWithdrawalRepository));
        }

        public async Task<CommandHandlingResult> Handle(Commands.SetLinkedCashInOperationCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(ForwardWithdrawalCommandHandler), nameof(Commands.SetLinkedCashInOperationCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            await _forwardWithdrawalRepository.SetLinkedCashInOperationId(command.Message.ClientId, command.Id, command.Id); // todo: remove this legacy ? LW?

            eventPublisher.PublishEvent(new ForwardWithdawalLinkedEvent { Message = command.Message });

            return CommandHandlingResult.Ok();
        }
    }
}