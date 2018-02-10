using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Service.OperationsRepository.AutorestClient.Models;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    public interface IClientTradesFactory
    {
        Task<ClientTrade[]> Create(string orderId, string clientId, string assetPairId, TradeQueueItem.TradeInfo trade, double marketVolume, double limitVolume);
    }
} 