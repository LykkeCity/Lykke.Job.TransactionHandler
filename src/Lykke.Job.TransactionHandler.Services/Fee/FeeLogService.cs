using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Domain.Fee;
using Lykke.Job.TransactionHandler.Core.Services.Fee;

namespace Lykke.Job.TransactionHandler.Services.Fee
{
    public class FeeLogService : IFeeLogService
    {
        private readonly IFeeLogRepository _feelogRepository;

        public FeeLogService(IFeeLogRepository feelogRepository)
        {
            _feelogRepository = feelogRepository ?? throw new ArgumentNullException(nameof(feelogRepository));
        }

        public async Task WriteFeeInfo(CashInOutQueueMessage feeDataSource)
        {
            var newItem = new FeeLogEntry
            {
                OperationId = feeDataSource.Id,
                Type = FeeOperationType.CashInOut,
                Transfers = feeDataSource.FeeTransfers?.ToJson(),
                Instructions = feeDataSource.FeeInstructions?.ToJson(),
                Data = string.Empty,
                Settings = string.Empty
            };

            await _feelogRepository.CreateAsync(newItem);
        }

        public async Task WriteFeeInfo(TransferQueueMessage feeDataSource)
        {
            var newItem = new FeeLogEntry
            {
                OperationId = feeDataSource.Id,
                Type = FeeOperationType.Transfer,
                Transfers = string.Empty,
                Instructions = string.Empty,
                Data = feeDataSource.FeeData?.ToJson(),
                Settings = feeDataSource.FeeSettings?.ToJson()
            };

            await _feelogRepository.CreateAsync(newItem);
        }

        public async Task WriteFeeInfo(TradeQueueItem feeDataSource)
        {
            var tasks = feeDataSource.Trades.Select(x =>
            {
                var newItem = new FeeLogEntry
                {
                    OperationId = feeDataSource.Order.Id,
                    Instructions = x.FeeInstruction?.ToJson(),
                    Transfers = x.FeeTransfer?.ToJson(),
                    Settings = string.Empty,
                    Data = string.Empty,
                    Type = FeeOperationType.Trade
                };

                return _feelogRepository.CreateAsync(newItem);
            });

            await Task.WhenAll(tasks);
        }

        public async Task WriteFeeInfo(IEnumerable<LimitQueueItem.LimitOrderWithTrades> feeDataSource)
        {
            foreach (var order in feeDataSource)
            {
                var tasks = order.Trades.Select(x =>
                {
                    var newItem = new FeeLogEntry
                    {
                        OperationId = order.Order.Id,
                        Instructions = x.FeeInstruction?.ToJson(),
                        Transfers = x.FeeTransfer?.ToJson(),
                        Settings = string.Empty,
                        Data = string.Empty,
                        Type = FeeOperationType.LimitTrade
                    };

                    return _feelogRepository.CreateAsync(newItem);
                });

                await Task.WhenAll(tasks);
            }
        }
    }
}