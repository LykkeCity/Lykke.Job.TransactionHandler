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
        Task CreateOrUpdateAsync(ILimitOrder limitOrder);
        Task<ILimitOrder> GetOrderAsync(string orderId);
        Task<IEnumerable<ILimitOrder>> GetActiveOrdersAsync(string clientId);
    }
}
