using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Fee
{
    public interface IOrderFeeLog
    {
        string OrderId { get; set; }
        string FeeInstruction { get; set; }
        string FeeTransfer { get; set; }
        string Type { get; set; }
        string OrderStatus { get; set; }
    }

    public class OrderFeeLog : IOrderFeeLog
    {
        public string OrderId { get; set; }
        public string FeeInstruction { get ; set; }
        public string FeeTransfer { get; set; }
        public string Type { get; set; }
        public string OrderStatus { get; set; }
    }

    public interface IFeeLogRepository
    {
        Task CreateAsync(IOrderFeeLog item);
    }
}