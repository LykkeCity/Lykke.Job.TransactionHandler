using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Fee
{
    public interface IFeeLogEntry
    {
        string OperationId { get; set; }
        FeeOperationType Type { get; set; }
        string Instructions { get; set; }
        string Transfers { get; set; }
        string Data { get; set; }
        string Settings { get; set; }

    }

    public class FeeLogEntry : IFeeLogEntry
    {
        public string OperationId { get; set; }
        public FeeOperationType Type { get; set; }
        public string Instructions { get; set; }
        public string Transfers { get; set; }
        public string Data { get; set; }
        public string Settings { get; set; }
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