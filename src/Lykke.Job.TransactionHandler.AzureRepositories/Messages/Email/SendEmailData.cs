namespace Lykke.Job.TransactionHandler.AzureRepositories.Messages.Email
{
    public class SendEmailData<T>
    {
        public string PartnerId { get; set; }
        public string EmailAddress { get; set; }
        public T MessageData { get; set; }

        public static SendEmailData<T> Create(string partnerId, string emailAddress, T msgData)
        {
            return new SendEmailData<T>
            {
                PartnerId = partnerId,
                EmailAddress = emailAddress,
                MessageData = msgData
            };
        }
    }
}