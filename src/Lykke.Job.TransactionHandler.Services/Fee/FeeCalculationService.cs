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

        public async Task<decimal> GetAmountNoFeeAsync(decimal initialAmount, string assetId, List<Core.Contracts.Fee> fees, string clientId = null)
        {
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(assetId);

            var fee = fees?.FirstOrDefault();

            var feeTransfer = fee?.Transfer;

            var feeAmount = (feeTransfer?.Volume ?? 0).TruncateDecimalPlaces(asset.Accuracy, true);

            if (fee?.Instruction?.Type == Core.Contracts.FeeType.EXTERNAL_FEE)
            {
                // if transfer ToClientId and fee TargetClient are the same client
                // this case used, for example, during Swift withdrawal return from BO to a client
                // fee is returned from one client, main sum is returned from another client
                // to a withdrawal original client
                if (clientId != null && clientId == fee.Instruction.TargetClientId)  
                {
                    return initialAmount + feeAmount;
                }
                return initialAmount;
            }

            return initialAmount > 0 ? initialAmount - feeAmount : initialAmount + feeAmount;
        }
    }
}
