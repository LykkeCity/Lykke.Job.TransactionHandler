using System.Threading.Tasks;
using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Clients
{
    public class ClientCacheEntity : BaseEntity, IClientCache
    {
        public static string GeneratePartitionKey()
        {
            return "ClientCache";
        }

        public int LimitOrdersCount { get; set; }

        public static ClientCacheEntity Create(string clientId)
        {
            return new ClientCacheEntity
            {
                PartitionKey = GeneratePartitionKey(),
                RowKey = clientId
            };
        }
    }

    public class ClientCacheRepository : IClientCacheRepository
    {
        private readonly INoSQLTableStorage<ClientCacheEntity> _storage;

        public ClientCacheRepository(INoSQLTableStorage<ClientCacheEntity> storage)
        {
            _storage = storage;
        }

        public async Task<IClientCache> GetCache(string clientId)
        {
            return await _storage.GetDataAsync(ClientCacheEntity.GeneratePartitionKey(), clientId) ?? new ClientCacheEntity();
        }

        public Task UpdateLimitOrdersCount(string clientId, int count)
        {
            var entity = ClientCacheEntity.Create(clientId);

            entity.LimitOrdersCount = count;

            return _storage.InsertOrMergeAsync(entity);
        }
    }
}
