namespace Lykke.Job.TransactionHandler.Core.Services.Messages.Email.ContentGenerator.MessagesData
{
    public class SolarCashOutData : IEmailMessageData
    {
        public string AddressTo { get; set; }
        public decimal Amount { get; set; }

        public string MessageId()
        {
            return "SolarCashOut";
        }
    }
}