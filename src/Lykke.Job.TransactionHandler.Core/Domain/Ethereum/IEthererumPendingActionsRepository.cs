using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Ethereum
{
    public interface IEthererumPendingActionsRepository
    {
        Task<IEnumerable<string>> GetPendingAsync(string clientId);

        Task CreateAsync(string clientId, string operationId);

        Task CompleteAsync(string clientId, string operationId);
    }
}
