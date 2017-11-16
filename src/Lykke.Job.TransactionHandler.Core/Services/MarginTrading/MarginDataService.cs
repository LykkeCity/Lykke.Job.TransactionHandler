using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Services.MarginTrading
{
    public interface IMarginDataService
    {
        Task<OperationResult> DepositToAccount(string clientId, string accountId, double amount, MarginPaymentType paymentType);
        Task<OperationResult> WithdrawFromAccount(string clientId, string accountId, double amount, MarginPaymentType paymentType);
    }

    public enum MarginPaymentType
    {
        Transfer,
        Swift
    }

    public class OperationResult
    {
        private OperationResult()
        {
            
        }

        public static OperationResult Error(string error)
        {
            return new OperationResult
            {
                ErrorMessage = error
            };
        }
        
        public static OperationResult Success()
        {
            return new OperationResult();
        }

        public string ErrorMessage { get; private set; }

        public bool IsOk => string.IsNullOrEmpty(ErrorMessage);
    }
}