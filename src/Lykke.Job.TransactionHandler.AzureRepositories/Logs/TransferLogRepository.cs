using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Logs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Logs
{
    public class TransferLogEntity : BaseEntity, ITransferLog
    {
        public string TransferId { get; set; }
        public DateTime TransferDate { get; set; }
        public string FromClientId { get; set; }
        public string ToClientid { get; set; }
        public string AssetId { get; set; }
        public string Amount { get; set; }
        public string FeeInstruction { get; set; }
        public string FeeTransfer { get; set; }

        public static TransferLogEntity Create(string transferId, DateTime transferDate, string fromClientId, string toClientid, string assetId, string amount, string feeInstruction, string feeTransfer)
        {
            return new TransferLogEntity()
            {
                PartitionKey = DateTime.Today.ToString("yyyy-MM-dd"),

                TransferId = transferId,
                TransferDate = transferDate,
                FromClientId = fromClientId,
                ToClientid = toClientid,
                AssetId = assetId,
                Amount = amount,
                FeeInstruction = feeInstruction,
                FeeTransfer = feeTransfer
            };
        }
    }

    public class TransferLogRepository : ITransferLogRepository
    {
        private readonly INoSQLTableStorage<TransferLogEntity> _tableStorage;

        public TransferLogRepository(INoSQLTableStorage<TransferLogEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task CreateAsync(string transferId, DateTime transferDate, string fromClientId, string toClientid, string assetId, string amount, string feeInstruction, string feeTransfer)
        {
            var entity = TransferLogEntity.Create(transferId, transferDate, fromClientId, toClientid, assetId, amount, feeInstruction, feeTransfer);
            await _tableStorage.InsertAndGenerateRowKeyAsTimeAsync(entity, DateTime.UtcNow);
        }
    }
}
