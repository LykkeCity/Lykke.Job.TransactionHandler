
namespace Lykke.Job.TransactionHandler.Core.Services.MarginTrading
{
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
    }
}