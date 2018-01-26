using System;
using System.Threading.Tasks;
using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Fee;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Fee
{
    public class FeeLogEntryEntity : TableEntity, IFeeLogEntry
    {
        public string Id => RowKey;
        public string Instructions { get; set; }
        public string Transfers { get; set; }
        public string Data { get; set; }
        public string Settings { get; set; }
        public FeeOperationType Type { get; set; }
        public string OperationId { get; set; }

        public static FeeLogEntryEntity Create(IFeeLogEntry item)
        {
            return new FeeLogEntryEntity
            {
                PartitionKey = GeneratePartitionKey(),
                RowKey = GenerateRowKey(),
                OperationId = item.OperationId,
                Type = item.Type,
                Data = item.Data,
                Instructions = item.Instructions,
                Settings = item.Settings,
                Transfers = item.Transfers
            };
        }

        public static string GeneratePartitionKey()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        public static string GenerateRowKey()
        {
            return Guid.NewGuid().ToString();
        }
    }

    public class FeeLogRepository : IFeeLogRepository
    {
        private readonly INoSQLTableStorage<FeeLogEntryEntity> _tableStorage;

        public FeeLogRepository(INoSQLTableStorage<FeeLogEntryEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task CreateAsync(IFeeLogEntry item)
        {
            await _tableStorage.InsertAsync(FeeLogEntryEntity.Create(item));
        }
    }
}