using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Ethereum
{
    public interface IEthereumCashinAggregateRepository
    {
        Task<IEthereumCashinAggregate> GetOrAddAsync(string trHash, Func<IEthereumCashinAggregate> newAggregateFactory);
        Task<IEthereumCashinAggregate> GetAsync(string trHash);
        Task SaveAsync(IEthereumCashinAggregate aggregate);
        Task<IEthereumCashinAggregate> TryGetAsync(string trHash);
    }
}