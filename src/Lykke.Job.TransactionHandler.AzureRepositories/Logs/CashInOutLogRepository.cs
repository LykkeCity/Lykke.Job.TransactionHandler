using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Logs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Logs
{
    public class CashInOutLogEntity : BaseEntity, ICashInOutLog
    {
        public string TransactionId { get; set; }
        public string ClientId { get; set; }
        public DateTime DateTime { get; set; }
        public string Volume { get; set; }
        public string Asset { get; set; }
        public string FeeInstructions { get; set; }
        public string FeeTransfers { get; set; }

        public static CashInOutLogEntity Create(string transactionId, string clientId, DateTime dateTime, string volume, string asset, string feeInstructions, string feeTransfers)
        {
            return new CashInOutLogEntity()
            {
                PartitionKey = DateTime.Today.ToString("yyyy-MM-dd"),

                TransactionId = transactionId,
                ClientId = clientId,
                DateTime = dateTime,
                Volume = volume,
                Asset = asset,
                FeeInstructions = feeInstructions,
                FeeTransfers = feeTransfers
            };
        }
    }

    public class CashInOutLogRepository : ICashInOutLogRepository
    {
        private readonly INoSQLTableStorage<CashInOutLogEntity> _tableStorage;

        public CashInOutLogRepository(INoSQLTableStorage<CashInOutLogEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task CreateAsync(string transactionId, string clientId, DateTime dateTime, string volume, string asset, string feeInstructions, string feeTransfers)
        {
            var entity = CashInOutLogEntity.Create(transactionId, clientId, dateTime, volume, asset, feeInstructions, feeTransfers);
            await _tableStorage.InsertAndGenerateRowKeyAsTimeAsync(entity, DateTime.UtcNow);
        }
    }
}
