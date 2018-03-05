using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Lykke.Job.TransactionHandler.Core.Services.Fee;
using Lykke.Service.Assets.Client;

namespace Lykke.Job.TransactionHandler.Services.Fee
{
    public class FeeCalculationService : IFeeCalculationService
    {
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;

        public FeeCalculationService(IAssetsServiceWithCache assetsServiceWithCache)
        {
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
        }

        public async Task<decimal> GetAmountNoFeeAsync(decimal initialAmount, string assetId, List<Core.Contracts.Fee> fees)
        {
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(assetId);

            var feeTransfer = fees?.FirstOrDefault()?.Transfer;

            var feeAmount = (feeTransfer?.Volume ?? 0).TruncateDecimalPlaces(asset.Accuracy, true);

            return  initialAmount > 0 ? initialAmount - feeAmount : initialAmount + feeAmount;
        }
    }
}