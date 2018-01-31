using Lykke.Service.OperationsRepository.AutorestClient.Models;
using System.Linq;
using Lykke.Job.TransactionHandler.Core.Contracts;
using FeeType = Lykke.Service.OperationsRepository.AutorestClient.Models.FeeType;

namespace Lykke.Job.TransactionHandler.Utils
{
    public static class CashInOutOperationExt
    {
        public static void AddFeeDataToOperation(this CashInOutOperation operation, CashInOutQueueMessage message)
        {
            operation.FeeSize = message?.Fees?.FirstOrDefault()?.Transfer?.Volume ?? 0d;
            operation.FeeType = FeeType.Absolute;
        }
    }
}