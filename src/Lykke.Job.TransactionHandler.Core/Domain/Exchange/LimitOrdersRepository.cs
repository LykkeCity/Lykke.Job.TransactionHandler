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
        Task<ILimitOrder> GetOrderAsync(string clientId, string orderId);
        Task<int> GetActiveOrdersCountAsync(string clientId);
    }
}
