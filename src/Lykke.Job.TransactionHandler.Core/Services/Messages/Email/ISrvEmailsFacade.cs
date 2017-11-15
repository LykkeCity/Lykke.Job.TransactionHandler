using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Services.Messages.Email
{
    public interface ISrvEmailsFacade
    {
        Task SendNoRefundDepositDoneMail(string partnerId, string email, double amount, string assetBcnId);

        Task SendNoRefundOCashOutMail(string partnerId, string email, double amount, string assetId, string bcnHash);

        Task SendTransferCompletedEmail(string partnerId, string email, string clientName, string assetId, double amountFiat,
            double amountLkk, double price, string srcHash);

        Task SendDirectTransferCompletedEmail(string partnerId, string email, string clientName, string assetId, double amount, string srcHash);

        Task SendSolarCashOutCompletedEmail(string partnerId, string email, string addressTo, double amount);
    }
}