namespace Lykke.Job.TransactionHandler.Core.Services.Messages.Email.ContentGenerator.MessagesData
{
    public class DirectTransferCompletedData : IEmailMessageData
    {
        public string ClientName { get; set; }
        public decimal Amount { get; set; }
        public string AssetId { get; set; }
        public string SrcBlockchainHash { get; set; }

        public string MessageId()
        {
            return "DirectTransferCompletedData";
        }
    }
}