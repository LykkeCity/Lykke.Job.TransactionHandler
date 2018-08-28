using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.ContentGenerator.MessagesData;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.Sender;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Services.Messages.Email
{
    public class SrvEmailsFacade : ISrvEmailsFacade
    {
        private readonly IEmailSender _emailSender;

        public SrvEmailsFacade(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        public async Task SendNoRefundDepositDoneMail(string partnerId, string email, decimal amount, string assetBcnId)
        {
            var msgData = new NoRefundDepositDoneData
            {
                Amount = amount,
                AssetBcnId = assetBcnId
            };
            await _emailSender.SendEmailAsync(partnerId, email, msgData);
        }

        public async Task SendNoRefundOCashOutMail(string partnerId, string email, decimal amount, string assetId, string bcnHash)
        {
            var msgData = new NoRefundOCashOutData
            {
                Amount = amount,
                AssetId = assetId,
                SrcBlockchainHash = bcnHash
            };

            await _emailSender.SendEmailAsync(partnerId, email, msgData);
        }
    }
}