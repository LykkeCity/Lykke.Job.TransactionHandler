using System;
using System.Collections.Generic;
using System.Linq;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.MatchingEngine.Connector.Models.Events.Common;
using Fee = Lykke.Job.TransactionHandler.Core.Contracts.Fee;
using FeeInstruction = Lykke.Job.TransactionHandler.Core.Contracts.FeeInstruction;
using FeeTransfer = Lykke.Job.TransactionHandler.Core.Contracts.FeeTransfer;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    internal static class FeeConversionExtensions
    {
        internal static List<Fee> ToOldFees(
            this List<MatchingEngine.Connector.Models.Events.Common.FeeTransfer> feeTransfers,
            List<MatchingEngine.Connector.Models.Events.Common.FeeInstruction> feeInstructions,
            DateTime timestamp)
        {
            if (feeTransfers == null)
                return null;

            var result = new List<Fee>(feeTransfers.Count);

            foreach (var feeTransfer in feeTransfers)
            {
                var feeInstruction = feeInstructions?.FirstOrDefault(i => i.Index == feeTransfer.Index);
                if (feeInstruction == null && feeTransfer == null)
                    continue;

                result.Add(new Fee
                {
                    Instruction = ToOld(feeInstruction),
                    Transfer = ToOld(feeTransfer, timestamp)
                });
            }

            return result;
        }

        internal static List<Fee> ToOldFees(this List<MatchingEngine.Connector.Models.Events.Common.Fee> fees, DateTime timestamp)
        {
            return fees?
                .Select(fee =>
                    new Fee
                    {
                        Instruction = ToOld(fee.Instruction),
                        Transfer = ToOld(fee.Transfer, timestamp)
                    })
                .ToList();
        }

        private static FeeInstruction ToOld(MatchingEngine.Connector.Models.Events.Common.FeeInstruction feeInstruction)
        {
            if (feeInstruction == null)
                return null;

            return new FeeInstruction
            {
                Type = ToOld(feeInstruction.Type),
                SizeType = ToOld(feeInstruction.SizeType),
                Size = decimal.TryParse(feeInstruction.Size, out var size) ? size : (decimal?) null,
                MakerSizeType = ToOld(feeInstruction.MakerSizeType),
                MakerSize = decimal.TryParse(feeInstruction.MakerSize, out var makerSize) ? makerSize : (decimal?) null,
                SourceClientId = feeInstruction.SourceWalletd,
                TargetClientId = feeInstruction.TargetWalletId,
                AssetIds = feeInstruction.AssetsIds,
                MakerFeeModificator = decimal.TryParse(feeInstruction.MakerFeeModificator, out var modificator)
                    ? modificator
                    : (decimal?) null,
            };
        }

        private static FeeTransfer ToOld(MatchingEngine.Connector.Models.Events.Common.FeeTransfer feeTransfer, DateTime timestamp)
        {
            if (feeTransfer == null)
                return null;

            return new FeeTransfer
            {
                FromClientId = feeTransfer.SourceWalletId,
                ToClientId = feeTransfer.TargetWalletId,
                Date = timestamp,
                Asset = feeTransfer.AssetId,
                Volume = decimal.TryParse(feeTransfer.Volume, out var volume) ? volume : 0,
                FeeCoef = decimal.TryParse(feeTransfer.FeeCoef, out var coef) ? coef : (decimal?)null,
            };
        }

        private static FeeSizeType ToOld(FeeInstructionSizeType feeInstructionSizeType)
        {
            switch (feeInstructionSizeType)
            {
                case FeeInstructionSizeType.Percentage:
                    return FeeSizeType.PERCENTAGE;
                case FeeInstructionSizeType.Absolute:
                    return FeeSizeType.ABSOLUTE;
                default:
                    throw new ArgumentOutOfRangeException(nameof(feeInstructionSizeType), feeInstructionSizeType, null);
            }
        }

        private static FeeType ToOld(FeeInstructionType feeInstructionType)
        {
            switch (feeInstructionType)
            {
                case FeeInstructionType.NoFee:
                    return FeeType.NO_FEE;
                case FeeInstructionType.WalletFee:
                    return FeeType.CLIENT_FEE;
                case FeeInstructionType.ExternalFee:
                    return FeeType.EXTERNAL_FEE;
                default:
                    throw new ArgumentOutOfRangeException(nameof(feeInstructionType), feeInstructionType, null);
            }
        }
    }
}
