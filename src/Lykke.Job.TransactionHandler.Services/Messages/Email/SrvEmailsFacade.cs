using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.ContentGenerator.MessagesData;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.Sender;

namespace Lykke.Job.TransactionHandler.Services.Messages.Email
{
    public class SrvEmailsFacade : ISrvEmailsFacade
    {
        private readonly IEmailSender _emailSender;

        public SrvEmailsFacade(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        public async Task SendNoRefundDepositDoneMail(string partnerId, string email, double amount, string assetBcnId)
        {
            var msgData = new NoRefundDepositDoneData
            {
                Amount = amount,
                AssetBcnId = assetBcnId
            };
            await _emailSender.SendEmailAsync(partnerId, email, msgData);
        }

        public async Task SendNoRefundOCashOutMail(string partnerId, string email, double amount, string assetId, string bcnHash)
        {
            var msgData = new NoRefundOCashOutData
            {
                Amount = amount,
                AssetId = assetId,
                SrcBlockchainHash = bcnHash
            };

            await _emailSender.SendEmailAsync(partnerId, email, msgData);
        }

        public async Task SendTransferCompletedEmail(string partnerId, string email, string clientName, string assetId, double amountFiat,
            double amountLkk,
            double price, string srcHash)
        {
            var msgData = new TransferCompletedData
            {
                AssetId = assetId,
                AmountFiat = amountFiat,
                AmountLkk = amountLkk,
                Price = price,
                ClientName = clientName,
                SrcBlockchainHash = srcHash
            };
            await _emailSender.SendEmailAsync(partnerId, email, msgData);
        }

        public async Task SendDirectTransferCompletedEmail(string partnerId, string email, string clientName, string assetId,
            double amount, string srcHash)
        {
            var msgData = new DirectTransferCompletedData
            {
                AssetId = assetId,
                Amount = amount,
                ClientName = clientName,
                SrcBlockchainHash = srcHash
            };

            await _emailSender.SendEmailAsync(partnerId, email, msgData);
        }

        public Task SendSolarCashOutCompletedEmail(string partnerId, string email, string addressTo, double amount)
        {
            var msgData = new SolarCashOutData
            {
                AddressTo = addressTo,
                Amount = amount
            };

            return _emailSender.SendEmailAsync(partnerId, email, msgData);
        }
    }
}