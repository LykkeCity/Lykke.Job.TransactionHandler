using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Service.OperationsRepository.AutorestClient.Models;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    public interface IClientTradesFactory
    {
        Task<ClientTrade[]> Create(TradeQueueItem.MarketOrder order, List<TradeQueueItem.TradeInfo> trades, string clientId);
    }
}