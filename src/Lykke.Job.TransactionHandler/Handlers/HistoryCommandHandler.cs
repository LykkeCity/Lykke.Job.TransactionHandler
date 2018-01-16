using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.LimitTrades;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class HistoryCommandHandler
    {
        private readonly ILog _log;
        private readonly ILimitOrdersRepository _limitOrdersRepository;
        
        public HistoryCommandHandler(ILog log, ILimitOrdersRepository limitOrdersRepository)
        {
            _log = log;
            _limitOrdersRepository = limitOrdersRepository;            
        }

        [UsedImplicitly]
        public async Task<CommandHandlingResult> Handle(CreateOrUpdateLimitOrderCommand command, IEventPublisher eventPublisher)
        {           
            _log.WriteInfo(nameof(CreateOrUpdateLimitOrderCommand), JsonConvert.SerializeObject(command, Formatting.Indented), "CreateOrUpdateLimitOrderCommand");

            await _limitOrdersRepository.CreateOrUpdateAsync(command.LimitOrder);

            int activeOrdersCount = 0;

            if (!command.IsTrustedClient)
            {
                activeOrdersCount = (await _limitOrdersRepository.GetActiveOrdersAsync(command.LimitOrder.ClientId)).Count();
            }

            eventPublisher.PublishEvent(new LimitOrderSavedEvent
            {
                Id = command.LimitOrder.Id,
                ClientId = command.LimitOrder.ClientId,
                IsTrustedClient = command.IsTrustedClient,
                ActiveOrdersCount = activeOrdersCount
            });

            return CommandHandlingResult.Ok();
        }        
    }
}