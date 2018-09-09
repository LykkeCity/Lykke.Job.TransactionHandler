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
        public async Task<CommandHandlingResult> Handle(Commands.SetLinkedCashInOperationCommand command, IEventPublisher eventPublisher)
        {
            eventPublisher.PublishEvent(new ForwardWithdawalLinkedEvent { Message = command.Message });

            return CommandHandlingResult.Ok();
        }
    }
}