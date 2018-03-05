using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Services.Fee
{
    public interface IFeeCalculationService
    {
        Task<decimal> GetAmountNoFeeAsync(decimal initialAmount, string assetId, List<Contracts.Fee> fees);
    }
}