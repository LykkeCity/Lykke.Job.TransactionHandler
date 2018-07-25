using Lykke.Service.OperationsRepository.AutorestClient.Models;
using System.Linq;
using Lykke.Job.TransactionHandler.Core.Contracts;
using FeeType = Lykke.Service.OperationsRepository.AutorestClient.Models.FeeType;
using Common;
using Lykke.Service.Assets.Client.Models;

namespace Lykke.Job.TransactionHandler.Utils
{
    public static class TransferOperationExt
    {
        public static void AddFeeDataToOperation(this TransferEvent operation, TransferQueueMessage message, Asset asset)
        {
            var fee = message?.Fees?.FirstOrDefault();
            if (fee?.Instruction == null)
            {
                return;
            }

            if (fee.Instruction.SourceClientId == operation.ClientId)
            {
                operation.FeeSize = (double)(fee.Transfer?.Volume ?? 0);
                operation.FeeType = FeeType.Absolute;
            }
            if (fee.Instruction.TargetClientId == operation.ClientId)
            {
                // client receives more (amount + fee), amount should be increased
                operation.Amount = (operation.Amount + (double)(fee.Transfer?.Volume ?? 0)).TruncateDecimalPlaces(asset.Accuracy, true);
            }
        }
    }
}