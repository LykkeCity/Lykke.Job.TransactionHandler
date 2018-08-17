using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Ethereum
{
    public interface IEthereumCashinAggregateRepository
    {
        Task<EthereumCashinAggregate> GetOrAddAsync(string trHash, Func<EthereumCashinAggregate> newAggregateFactory);
        Task<EthereumCashinAggregate> GetAsync(string trHash);
        Task SaveAsync(EthereumCashinAggregate aggregate);
    }
}