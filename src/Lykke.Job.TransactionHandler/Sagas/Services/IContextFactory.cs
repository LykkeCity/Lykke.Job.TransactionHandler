using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Queues.Models;

namespace Lykke.Job.TransactionHandler.Sagas.Services
{
    public interface IContextFactory
    {
        Task<SwapOffchainContextData> FillTradeContext(SwapOffchainContextData context, TradeQueueItem.MarketOrder order, List<TradeQueueItem.TradeInfo> trades, string clientId);
    }
} 