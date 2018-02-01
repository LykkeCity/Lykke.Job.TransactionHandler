using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Contracts;

namespace Lykke.Job.TransactionHandler.Core.Services.Fee
{
    public interface IFeeLogService
    {
        Task WriteFeeInfoAsync(CashInOutQueueMessage feeDataSource);
        Task WriteFeeInfoAsync(TransferQueueMessage feeDataSource);
        Task WriteFeeInfoAsync(TradeQueueItem feeDataSource);
        Task WriteFeeInfoAsync(IEnumerable<LimitQueueItem.LimitOrderWithTrades> feeDataSource);
        Task WriteFeeInfoAsync(LimitQueueItem.LimitOrderWithTrades feeDataSource);
    }
}