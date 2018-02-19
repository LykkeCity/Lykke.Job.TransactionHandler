namespace Lykke.Job.TransactionHandler.Core.Services.Messages.Email.ContentGenerator.MessagesData
{
    public class NoRefundOCashOutData : IEmailMessageData
    {
        public string AssetId { get; set; }
        public decimal Amount { get; set; }
        public string SrcBlockchainHash { get; set; }
        public string MessageId()
        {
            return "NoRefundOCashOutEmail";
        }
    }
}