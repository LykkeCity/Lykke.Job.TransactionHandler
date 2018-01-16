using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Events.LimitOrders;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class LimitOrdersProjection
    {
        private readonly ILimitOrdersRepository _limitOrdersRepository;
        
        public LimitOrdersProjection(ILimitOrdersRepository limitOrdersRepository)
        {
            _limitOrdersRepository = limitOrdersRepository;            
        }

        public async Task Handle(LimitOrderExecutedEvent evt)
        {
            await _limitOrdersRepository.CreateOrUpdateAsync(evt.LimitOrder.Order);
        }
    }
}