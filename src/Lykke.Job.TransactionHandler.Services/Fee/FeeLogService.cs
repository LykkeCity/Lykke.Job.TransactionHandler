using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Domain.Fee;
using Lykke.Job.TransactionHandler.Core.Services.Fee;
using MoreLinq;

namespace Lykke.Job.TransactionHandler.Services.Fee
{
    public class FeeLogService : IFeeLogService
    {
        private readonly IFeeLogRepository _feelogRepository;

        private const int BatchPieceSize = 15;

        public FeeLogService(IFeeLogRepository feelogRepository)
        {
            _feelogRepository = feelogRepository ?? throw new ArgumentNullException(nameof(feelogRepository));
        }

        public async Task WriteFeeInfoAsync(CashInOutQueueMessage feeDataSource)
        {
            var newItem = new FeeLogEntry
            {
                OperationId = feeDataSource.Id,
                Type = FeeOperationType.CashInOut,
                Fee = feeDataSource.Fees?.ToJson()
            };

            await _feelogRepository.CreateAsync(newItem);
        }

        public async Task WriteFeeInfoAsync(TransferQueueMessage feeDataSource)
        {
            var newItem = new FeeLogEntry
            {
                OperationId = feeDataSource.Id,
                Type = FeeOperationType.Transfer,
                Fee = feeDataSource.Fees?.ToJson()
            };

            await _feelogRepository.CreateAsync(newItem);
        }

        public async Task WriteFeeInfoAsync(TradeQueueItem feeDataSource)
        {
            var tasks = feeDataSource.Trades.Select(x =>
            {
                var newItem = new FeeLogEntry
                {
                    OperationId = feeDataSource.Order.Id,
                    Fee = x.Fees?.ToJson(),
                    Type = FeeOperationType.Trade
                };

                return _feelogRepository.CreateAsync(newItem);
            });

            await Task.WhenAll(tasks);
        }

        public async Task WriteFeeInfoAsync(IEnumerable<LimitQueueItem.LimitOrderWithTrades> feeDataSource)
        {
            foreach (var order in feeDataSource)
            {
                await WriteFeeInfoAsync(order);
            }
        }

        public async Task WriteFeeInfoAsync(LimitQueueItem.LimitOrderWithTrades feeDataSource)
        {
            foreach (var batch in feeDataSource.Trades.Batch(BatchPieceSize))
            {
                var tasks = batch.Select(x =>
                {
                    var newItem = new FeeLogEntry
                    {
                        OperationId = feeDataSource.Order.Id,
                        Fee = x.Fees?.ToJson(),
                        Type = FeeOperationType.LimitTrade
                    };

                    return _feelogRepository.CreateAsync(newItem);
                });

                await Task.WhenAll(tasks);
            }
        }
    }
}