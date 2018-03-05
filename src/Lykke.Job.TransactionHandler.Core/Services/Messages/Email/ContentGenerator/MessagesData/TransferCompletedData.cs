namespace Lykke.Job.TransactionHandler.Core.Services.Messages.Email.ContentGenerator.MessagesData
{
    public class TransferCompletedData : IEmailMessageData
    {
        public string ClientName { get; set; }
        public decimal AmountFiat { get; set; }
        public decimal AmountLkk { get; set; }
        public decimal Price { get; set; }
        public string AssetId { get; set; }
        public string SrcBlockchainHash { get; set; }
        public string MessageId()
        {
            return "TransferCompletedEmail";
        }
    }
}