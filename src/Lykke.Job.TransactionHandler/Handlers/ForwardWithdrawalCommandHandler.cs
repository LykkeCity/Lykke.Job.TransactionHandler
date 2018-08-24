using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class ForwardWithdrawalCommandHandler
    {
        private readonly IForwardWithdrawalRepository _forwardWithdrawalRepository;

        public ForwardWithdrawalCommandHandler(
            [NotNull] IForwardWithdrawalRepository forwardWithdrawalRepository)
        {
            _forwardWithdrawalRepository = forwardWithdrawalRepository ?? throw new ArgumentNullException(nameof(forwardWithdrawalRepository));
        }

        public async Task<CommandHandlingResult> Handle(Commands.SetLinkedCashInOperationCommand command, IEventPublisher eventPublisher)
        {
            await _forwardWithdrawalRepository.SetLinkedCashInOperationId(command.Message.ClientId, command.Id, command.Id); // todo: remove this legacy ? LW?

            ChaosKitty.Meow();

            eventPublisher.PublishEvent(new ForwardWithdawalLinkedEvent { Message = command.Message });

            return CommandHandlingResult.Ok();
        }
    }
}