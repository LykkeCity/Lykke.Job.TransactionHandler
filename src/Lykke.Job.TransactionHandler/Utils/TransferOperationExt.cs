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
            if (fee == null)
            {
                return;
            }
            if (fee.Instruction.Type == Core.Contracts.FeeType.EXTERNAL_FEE)
            {
                if (operation.ClientId == fee.Instruction.TargetClientId)
                // amount target client id and EXTERNAL_FEE target client id is the same
                // for exapmle - swift cashout attempt rejected
                // main amount is transferred to the client from hotwallet
                // fee amount is transferred to the client from fee wallet as external fee
                {
                    operation.Amount = (operation.Amount + (double)(fee.Transfer?.Volume ?? 0)).TruncateDecimalPlaces(asset.Accuracy, true);
                }
            }
            else
            {
                operation.FeeSize = (double)(fee.Transfer?.Volume ?? 0);
                operation.FeeType = FeeType.Absolute;
            }
        }
    }
}