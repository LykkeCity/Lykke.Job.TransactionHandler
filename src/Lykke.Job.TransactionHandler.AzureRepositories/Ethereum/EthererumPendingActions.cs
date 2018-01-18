using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Microsoft.WindowsAzure.Storage.Table;
using System;
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
    }

    public class EthererumPendingActionsRepository : IEthererumPendingActionsRepository
    {
        private readonly INoSQLTableStorage<EthererumPendingActionEntity> _tableStorage;

        public EthererumPendingActionsRepository(INoSQLTableStorage<EthererumPendingActionEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task CreateAsync(string clientId, string operationId)
        {
            var entity = EthererumPendingActionEntity.CreatePending(clientId, operationId);

            await _tableStorage.InsertAsync(entity);
        }
    }
}
