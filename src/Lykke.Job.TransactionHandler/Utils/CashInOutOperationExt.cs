using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FeeType = Lykke.Service.OperationsRepository.AutorestClient.Models.FeeType;

namespace Lykke.Job.TransactionHandler.Utils
{
    public static class CashInOutOperationExt
    {
        public static void AddFeeDataToOperation(this CashInOutOperation operation, CashInOutQueueMessage message, ILog log)
        {
            var feeSize = 0.0;
            var feeType = FeeType.Absolute;

            var feeInstruction = message?.FeeInstructions?.FirstOrDefault();
            if (feeInstruction != null)
            {
                feeSize = feeInstruction.Size ?? 0.0;
                if (feeSize > 0)
                {
                    switch (feeInstruction.SizeType)
                    {
                        case FeeSizeType.ABSOLUTE:
                            feeType = FeeType.Absolute; break;
                        case FeeSizeType.PERCENTAGE:
                            feeType = FeeType.Relative; break;
                        default:
                            log.WriteWarningAsync(nameof(CashInOutOperationExt), nameof(AddFeeDataToOperation), new { operation, message }.ToJson(), $"Unknown fee size type: {feeInstruction.SizeType}. Add logic for converting to FeeType (FeeType.Absolute used as default).")
                               .Wait();
                            feeType = FeeType.Absolute;
                            
                            break;
                    }
                }
            }

            operation.FeeSize = feeSize;
            operation.FeeType = feeType;
        }
    }
}
