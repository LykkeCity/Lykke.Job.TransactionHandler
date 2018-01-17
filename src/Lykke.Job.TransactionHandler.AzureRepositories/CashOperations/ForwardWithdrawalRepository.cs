using System;
using System.Threading.Tasks;
using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Job.TransactionHandler.AzureRepositories.CashOperations
{
    public class ForwardWithdrawalEntity : TableEntity, IForwardWithdrawal
    {
        public static string GeneratePartitionKey(string clientId)
        {
            return clientId;
        }

        public static string GenerateRowKey(string id)
        {
            return id;
        }

        public string Id { get; set; }
        public string AssetId { get; set; }
        public string ClientId { get; set; }
        public double Amount { get; set; }
        public DateTime DateTime { get; set; }
        public string CashInId { get; set; }
    }

    public class ForwardWithdrawalRepository : IForwardWithdrawalRepository
    {
        private readonly INoSQLTableStorage<ForwardWithdrawalEntity> _tableStorage;

        public ForwardWithdrawalRepository(INoSQLTableStorage<ForwardWithdrawalEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task SetLinkedCashInOperationId(string clientId, string id, string cashInId)
        {
            await _tableStorage.MergeAsync(ForwardWithdrawalEntity.GeneratePartitionKey(clientId),
                ForwardWithdrawalEntity.GenerateRowKey(id), entity =>
                {
                    entity.CashInId = cashInId;
                    return entity;
                });
        }
    }
}