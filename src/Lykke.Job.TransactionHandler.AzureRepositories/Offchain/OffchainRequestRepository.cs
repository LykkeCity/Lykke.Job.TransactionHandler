using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Offchain
{
    public class OffchainRequestEntity : BaseEntity, IOffchainRequest
    {
        public string RequestId => RowKey;
        public string TransferId { get; set; }

        public string AssetId { get; set; }

        public string ClientId { get; set; }

        public RequestType Type { get; set; }

        public DateTime? StartProcessing { get; set; }

        public DateTime CreateDt { get; set; }

        public int TryCount { get; set; }

        public OffchainTransferType TransferType { get; set; }

        public DateTime? ServerLock { get; set; }

        public static class ByRecord
        {
            private const string Partition = "OffchainSignatureRequestEntity";

            public static OffchainRequestEntity Create(string id, string transferId, string clientId, string assetId, RequestType type, OffchainTransferType transferType, DateTime? serverLock)
            {
                var item = CreateNew(transferId, clientId, assetId, type, transferType, serverLock);

                item.PartitionKey = Partition;
                item.RowKey = id;

                return item;
            }
        }

        public static class ByClient
        {
            public static string GeneratePartition(string clientId)
            {
                return clientId;
            }

            public static OffchainRequestEntity Create(string id, string transferId, string clientId, string assetId, RequestType type, OffchainTransferType transferType, DateTime? serverLock)
            {
                var item = CreateNew(transferId, clientId, assetId, type, transferType, serverLock);

                item.PartitionKey = GeneratePartition(clientId);
                item.RowKey = id;

                return item;
            }
        }

        public static OffchainRequestEntity CreateNew(string transferId, string clientId, string assetId, RequestType type, OffchainTransferType transferType, DateTime? serverLock = null)
        {
            return new OffchainRequestEntity
            {
                TransferId = transferId,
                ClientId = clientId,
                AssetId = assetId,
                Type = type,
                CreateDt = DateTime.UtcNow,
                TransferType = transferType,
                ServerLock = serverLock
            };
        }
    }

    public class OffchainRequestRepository : IOffchainRequestRepository
    {
        private readonly INoSQLTableStorage<OffchainRequestEntity> _table;

        public OffchainRequestRepository(INoSQLTableStorage<OffchainRequestEntity> table)
        {
            _table = table;
        }

        public async Task<IOffchainRequest> CreateRequest(string transferId, string clientId, string assetId, RequestType type, OffchainTransferType transferType, DateTime? serverLock = null)
        {
            var id = Guid.NewGuid().ToString();

            var byClient = OffchainRequestEntity.ByClient.Create(id, transferId, clientId, assetId, type, transferType, serverLock);
            await _table.InsertOrReplaceAsync(byClient);

            var byRecord = OffchainRequestEntity.ByRecord.Create(id, transferId, clientId, assetId, type, transferType, serverLock);
            await _table.InsertOrReplaceAsync(byRecord);

            return byRecord;
        }
    }
}
