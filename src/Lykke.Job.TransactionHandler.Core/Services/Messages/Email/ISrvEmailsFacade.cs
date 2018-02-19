using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Services.Messages.Email
{
    public interface ISrvEmailsFacade
    {
        Task SendNoRefundDepositDoneMail(string partnerId, string email, decimal amount, string assetBcnId);

        Task SendNoRefundOCashOutMail(string partnerId, string email, decimal amount, string assetId, string bcnHash);

        Task SendTransferCompletedEmail(string partnerId, string email, string clientName, string assetId, decimal amountFiat,
            decimal amountLkk, decimal price, string srcHash);

        Task SendDirectTransferCompletedEmail(string partnerId, string email, string clientName, string assetId, decimal amount, string srcHash);

        Task SendSolarCashOutCompletedEmail(string partnerId, string email, string addressTo, decimal amount);
    }
}