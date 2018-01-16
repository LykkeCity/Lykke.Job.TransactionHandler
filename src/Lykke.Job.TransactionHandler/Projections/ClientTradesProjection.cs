using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

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
            if (evt.Trades != null)
                await _clientTradeOperations.SaveAsync(evt.Trades
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
                    }).ToArray());
        }
    }
}