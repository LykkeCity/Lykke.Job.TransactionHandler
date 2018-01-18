using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Clients
{
    public interface IClientCache
    {
        int LimitOrdersCount { get; }
    }

    public interface IClientCacheRepository
    {
        Task UpdateLimitOrdersCount(string clientId, int count);
    }
}
