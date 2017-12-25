using System;
using System.Threading.Tasks;
using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Fee;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Fee
{
    public class FeeLogEntity : TableEntity, IOrderFeeLog
    {
        public string Id => RowKey;
        public string FeeInstruction { get; set; }
        public string FeeTransfer { get; set; }
        public string OrderId { get; set; }
        public string Type { get; set; }
        public string OrderStatus { get; set; }

        public static FeeLogEntity CreateNew(IOrderFeeLog item)
        {
            return new FeeLogEntity
            {
                PartitionKey = GeneratePartitionKey(),
                RowKey = GenerateRowKey(),
                FeeTransfer = item.FeeTransfer,
                FeeInstruction = item.FeeInstruction,
                OrderId = item.OrderId,
                Type = item.Type,
                OrderStatus = item.OrderStatus
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
        private readonly INoSQLTableStorage<FeeLogEntity> _tableStorage;

        public FeeLogRepository(INoSQLTableStorage<FeeLogEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task CreateAsync(IOrderFeeLog item)
        {
            await _tableStorage.InsertAsync(FeeLogEntity.CreateNew(item));
        }
    }
}