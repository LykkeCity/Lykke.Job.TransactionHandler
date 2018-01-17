using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Exchange
{
    public interface ILimitOrder : IOrderBase
    {
        double RemainingVolume { get; set; }
        string MatchingId { get; set; }
    }

    public interface ILimitOrdersRepository
    {
        Task CreateOrUpdateAsync(ILimitOrder marketOrder);
        Task<ILimitOrder> GetOrderAsync(string orderId);
        Task<IEnumerable<ILimitOrder>> GetOrdersAsync(IEnumerable<string> orderIds);
        Task<IEnumerable<ILimitOrder>> GetActiveOrdersAsync(string clientId);
        Task<IEnumerable<ILimitOrder>> GetOrdersAsync(string clientId);
    }
}