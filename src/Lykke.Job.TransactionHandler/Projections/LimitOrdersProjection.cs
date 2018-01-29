﻿using System.Threading.Tasks;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Job.TransactionHandler.Utils;
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
            await _limitOrdersRepository.CreateOrUpdateAsync(evt.LimitOrder.Order);

            _log.WriteInfo(nameof(LimitOrdersProjection), JsonConvert.SerializeObject(evt.LimitOrder.Order, Formatting.Indented), $"Client {evt.LimitOrder.Order.ClientId}. Limit order {evt.LimitOrder.Order.Id} updated.");

            ChaosKitty.Meow();
        }
    }
}