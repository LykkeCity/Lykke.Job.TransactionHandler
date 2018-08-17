using AzureStorage;
using AzureStorage.Tables;
using Common.Log;
using Lykke.Job.TransactionHandler.AzureRepositories.Ethereum.Entities;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.SettingsReader;
using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Ethereum
{
    public class EthereumCashinAggregateRepository : IEthereumCashinAggregateRepository
    {
        private readonly INoSQLTableStorage<EthereumCashinAggregateEntity> _storage;

        public static IEthereumCashinAggregateRepository Create(IReloadingManager<string> connectionString, ILog log)
        {
            var storage = AzureTableStorage<EthereumCashinAggregateEntity>.Create(
                connectionString,
                "EthereumCashinAggregate",
                log);

            return new EthereumCashinAggregateRepository(storage);
        }

        public EthereumCashinAggregateRepository(INoSQLTableStorage<EthereumCashinAggregateEntity> tableStorage)
        {
            _storage = tableStorage;
        }

        public async Task<EthereumCashinAggregate> GetAsync(string trHash)
        {
            var partitionKey = EthereumCashinAggregateEntity.GetPartitionKey(trHash);
            var rowKey = EthereumCashinAggregateEntity.GetRowKey(trHash);

            var entity = await _storage.GetDataAsync(
                partitionKey,
                rowKey);

            return entity.ToDomain();
        }

        public async Task<EthereumCashinAggregate> GetOrAddAsync(string trHash, Func<EthereumCashinAggregate> newAggregateFactory)
        {
            var partitionKey = EthereumCashinAggregateEntity.GetPartitionKey(trHash);
            var rowKey = EthereumCashinAggregateEntity.GetRowKey(trHash);

            var startedEntity = await _storage.GetOrInsertAsync(
                partitionKey,
                rowKey,
                () =>
                {
                    var newAggregate = newAggregateFactory();

                    return EthereumCashinAggregateEntity.FromDomain(newAggregate);
                });

            return startedEntity.ToDomain();
        }

        public async Task SaveAsync(EthereumCashinAggregate aggregate)
        {
            var entity = EthereumCashinAggregateEntity.FromDomain(aggregate);

            await _storage.ReplaceAsync(entity);
        }
    }
}
