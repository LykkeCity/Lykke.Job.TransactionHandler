using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class ClientTradesProjection
    {
        private readonly ILog _log;
        private readonly ITradeOperationsRepositoryClient _clientTradeOperations;

        public ClientTradesProjection(ILog log, ITradeOperationsRepositoryClient clientTradeOperations)
        {
            _log = log;
            _clientTradeOperations = clientTradeOperations;
        }

        public async Task Handle(LimitOrderExecutedEvent evt)
        {
            if (evt.Trades != null && evt.Trades.Any())
            {
                var clientTrades = evt.Trades
                    .Select(t => new ClientTrade
                    {
                        Id = t.Id,
                        ClientId = t.ClientId,
                        AssetId = t.AssetId,
                        Amount = t.Amount,
                        DateTime = t.DateTime,
                        Price = t.Price,
                        LimitOrderId = t.LimitOrderId,
                        OppositeLimitOrderId = t.OppositeLimitOrderId,
                        TransactionId = t.TransactionId,
                        IsLimitOrderResult = t.IsLimitOrderResult,
                        State = t.State
                    }).ToArray();

                await _clientTradeOperations.SaveAsync(clientTrades);

                _log.WriteInfo(nameof(ClientTradesProjection), JsonConvert.SerializeObject(clientTrades, Formatting.Indented), $"Client {evt.LimitOrder.Order.ClientId}. Limit trades {evt.LimitOrder.Order.Id}. Client trades saved.");
            }
            else
            {
                _log.WriteInfo(nameof(ClientTradesProjection), JsonConvert.SerializeObject(evt, Formatting.Indented), $"Client {evt.LimitOrder.Order.ClientId}. Limit order: {evt.LimitOrder.Order.Id}. Trades are empty");
            }
        }
    }
}