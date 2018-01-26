using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Contracts;

namespace Lykke.Job.TransactionHandler.Core.Services.Fee
{
    public interface IFeeLogService
    {
        Task WriteFeeInfo(CashInOutQueueMessage feeDataSource);
        Task WriteFeeInfo(TransferQueueMessage feeDataSource);
        Task WriteFeeInfo(TradeQueueItem feeDataSource);
        Task WriteFeeInfo(LimitQueueItem feeDataSource);
    }
}