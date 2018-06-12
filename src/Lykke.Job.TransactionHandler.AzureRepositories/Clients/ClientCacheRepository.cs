using System.Threading.Tasks;
using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Clients
{
    public class ClientCacheEntity : BaseEntity, IClientCache
    {
        public static string GeneratePartitionKey(string clientId)
        {
            return clientId;
        }

        public static string GenerateRowKey()
        {
            return "LimitOrdersCount";
        }

        public int LimitOrdersCount { get; set; }

        public static ClientCacheEntity Create(string clientId)
        {
            return new ClientCacheEntity
            {
                PartitionKey = GeneratePartitionKey(clientId),
                RowKey = GenerateRowKey(),
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

        public Task UpdateLimitOrdersCount(string clientId, int count)
        {
            var entity = ClientCacheEntity.Create(clientId);

            entity.LimitOrdersCount = count;

            return _storage.InsertOrMergeAsync(entity);
        }
    }
}
