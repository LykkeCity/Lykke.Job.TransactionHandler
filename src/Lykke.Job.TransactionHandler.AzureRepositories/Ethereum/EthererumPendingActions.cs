using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Ethereum
{
    public class EthererumPendingActionEntity : TableEntity
    {
        public static EthererumPendingActionEntity CreatePending(string clientId,
            string operationId)
        {
            return new EthererumPendingActionEntity
            {
                PartitionKey = clientId,
                RowKey = operationId,
                Timestamp = DateTimeOffset.UtcNow,
            };
        }

        public static EthererumPendingActionEntity CreateCompleted(string clientId,
            string operationId)
        {
            return new EthererumPendingActionEntity
            {
                PartitionKey = $"Completed_{clientId}",
                RowKey = operationId,
                Timestamp = DateTimeOffset.UtcNow,
            };
        }

        public string ClientId => PartitionKey;
        public string OperationId => RowKey;
    }

    public class EthererumPendingActionsRepository : IEthererumPendingActionsRepository
    {
        private readonly INoSQLTableStorage<EthererumPendingActionEntity> _tableStorage;

        public EthererumPendingActionsRepository(INoSQLTableStorage<EthererumPendingActionEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task<IEnumerable<string>> GetPendingAsync(string clientId)
        {
            var entities = await _tableStorage.GetDataAsync(clientId);

            return entities?.Select(x => x.OperationId);
        }

        public async Task CreateAsync(string clientId, string operationId)
        {
            var entity = EthererumPendingActionEntity.CreatePending(clientId, operationId);

            await _tableStorage.InsertAsync(entity);
        }

        public async Task CompleteAsync(string clientId, string operationId)
        {
            var entity = EthererumPendingActionEntity.CreateCompleted(clientId, operationId);

            await _tableStorage.InsertAsync(entity);
            await _tableStorage.DeleteIfExistAsync(clientId, operationId);

        }
    }
}
