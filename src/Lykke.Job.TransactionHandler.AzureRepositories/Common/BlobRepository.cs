using System.Threading.Tasks;
using AzureStorage;
using Common;
using Konscious.Security.Cryptography;
using Lykke.Job.TransactionHandler.Core.Domain.Common;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Common
{
    public class BlobRepository : IBlobRepository
    {
        private static readonly HMACBlake2B HashAlgorithm = new HMACBlake2B(128);

        private readonly INoSQLTableStorage<BlobEntity> _tableStorage;

        static BlobRepository()
        {
            HashAlgorithm.Initialize();
        }
        public BlobRepository(INoSQLTableStorage<BlobEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task<string> Insert(object value)
        {
            var entity = CreateEntity(value);

            await _tableStorage.InsertAsync(entity);

            return entity.RowKey;
        }

        private static BlobEntity CreateEntity(object value)
        {
            var content = NetJSON.NetJSON.Serialize(value);
            var key = HashAlgorithm.ComputeHash(content.ToUtf8Bytes()).ToHexString();
            return new BlobEntity
            {
                PartitionKey = value.GetType().Name,
                RowKey = key,
                Content = content
            };
        }
    }

    public class BlobEntity : TableEntity
    {
        public string Content { get; set; }
    }
}
