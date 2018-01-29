using System.Threading.Tasks;
using Common;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Services.Fee;

namespace Lykke.Job.TransactionHandler.Services
{
    public static class ServiceExtensions
    {
        public static async Task<double> GetAmountNoFee(this IFeeCalculationService src, CashInOutQueueMessage message)
        {
            return await src.GetAmountNoFee(message.Amount.ParseAnyDouble(), message.AssetId, message.Fees);
        }

        public static async Task<double> GetAmountNoFee(this IFeeCalculationService src, TransferQueueMessage message)
        {
            return await src.GetAmountNoFee(message.Amount.ParseAnyDouble(), message.AssetId, message.Fees);
        }
    }
}