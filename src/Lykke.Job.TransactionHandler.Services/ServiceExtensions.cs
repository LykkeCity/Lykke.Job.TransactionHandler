using System.Threading.Tasks;
using Common;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Services.Fee;

namespace Lykke.Job.TransactionHandler.Services
{
    public static class ServiceExtensions
    {
        public static Task<double> GetAmountNoFeeAsync(this IFeeCalculationService src, CashInOutQueueMessage message)
        {
            return src.GetAmountNoFeeAsync(message.Amount.ParseAnyDouble(), message.AssetId, message.Fees);
        }

        public static Task<double> GetAmountNoFeeAsync(this IFeeCalculationService src, TransferQueueMessage message)
        {
            return src.GetAmountNoFeeAsync(message.Amount.ParseAnyDouble(), message.AssetId, message.Fees);
        }
    }
}