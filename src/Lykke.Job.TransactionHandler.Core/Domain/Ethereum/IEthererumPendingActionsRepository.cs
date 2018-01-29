using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Ethereum
{
    public interface IEthererumPendingActionsRepository
    {
        Task CreateAsync(string clientId, string operationId);
    }
}
