using System.Threading.Tasks;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class LimitOrdersProjection
    {
        private readonly ILog _log;
        private readonly ILimitOrdersRepository _limitOrdersRepository;
        
        public LimitOrdersProjection(ILog log, ILimitOrdersRepository limitOrdersRepository)
        {
            _log = log;
            _limitOrdersRepository = limitOrdersRepository;
        }

        public async Task Handle(LimitOrderExecutedEvent evt)
        {
            _log.WriteInfo(nameof(LimitOrdersProjection), JsonConvert.SerializeObject(evt, Formatting.Indented), "LimitOrderExecutedEvent");

            await _limitOrdersRepository.CreateOrUpdateAsync(evt.LimitOrder.Order);
        }
    }
}