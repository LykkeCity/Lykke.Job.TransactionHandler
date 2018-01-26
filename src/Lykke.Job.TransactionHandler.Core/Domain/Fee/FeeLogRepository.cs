using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Fee
{
    public interface IFeeLogEntry
    {
        string OperationId { get; set; }
        FeeOperationType Type { get; set; }
        string Fee { get; set; }

    }

    public class FeeLogEntry : IFeeLogEntry
    {
        public string OperationId { get; set; }
        public FeeOperationType Type { get; set; }
        public string Fee { get; set; }
    }

    public enum FeeOperationType
    {
        CashInOut = 0,
        Trade,
        Transfer,
        LimitTrade
    }

    public interface IFeeLogRepository
    {
        Task CreateAsync(IFeeLogEntry item);
    }
}