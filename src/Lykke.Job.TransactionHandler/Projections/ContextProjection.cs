using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Job.TransactionHandler.Queues.Models;
using Newtonsoft.Json;
using AggregatedTransfer = Lykke.Job.TransactionHandler.Handlers.AggregatedTransfer;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class ContextProjection
    {
        private readonly ILog _log;
        private readonly ITransactionService _bitcoinTransactionService;

        public ContextProjection(ILog log, ITransactionService bitcoinTransactionService)
        {
            _log = log;
            _bitcoinTransactionService = bitcoinTransactionService;
        }

        public async Task Handle(LimitOrderExecutedEvent evt)
        {           
            if (evt.IsTrustedClient)
                return;
            
            var contextData = await _bitcoinTransactionService.GetTransactionContext<SwapOffchainContextData>(evt.LimitOrder.Order.Id) ?? new SwapOffchainContextData();

            var aggregated = evt.Aggregated ?? new List<AggregatedTransfer>();

            foreach (var operation in aggregated.Where(x => x.ClientId == evt.LimitOrder.Order.ClientId))
            {
                var trade = evt.Trades.FirstOrDefault(x => x.ClientId == operation.ClientId && x.AssetId == operation.AssetId && Math.Abs(x.Amount - (double)operation.Amount) < 0.00000001);

                contextData.Operations.Add(new SwapOffchainContextData.Operation()
                {
                    TransactionId = operation.TransferId,
                    Amount = operation.Amount,
                    ClientId = operation.ClientId,
                    AssetId = operation.AssetId,
                    ClientTradeId = trade?.Id
                });
            }

            await _bitcoinTransactionService.CreateOrUpdateAsync(evt.LimitOrder.Order.Id);
            await _bitcoinTransactionService.SetTransactionContext(evt.LimitOrder.Order.Id, contextData);

            _log.WriteInfo(nameof(ContextProjection), JsonConvert.SerializeObject(contextData), $"Client {evt.LimitOrder.Order.ClientId}. Limit order {evt.LimitOrder.Order.Id}. Context updated.");
        }        
    }
}