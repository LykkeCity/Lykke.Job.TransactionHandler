using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Domain.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.ContentGenerator.MessagesData;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.Sender;

namespace Lykke.Job.TransactionHandler.Services.Messages.Email
{
    public class EmailSender : IEmailSender
    {
        private readonly IEmailCommandProducer _emailCommandProducer;

        public EmailSender(IEmailCommandProducer emailCommandProducer)
        {
            _emailCommandProducer = emailCommandProducer;
        }

        public async Task SendEmailAsync<T>(string partnerId, string email, T messageData) where T : IEmailMessageData
        {
            await _emailCommandProducer.ProduceSendEmailCommand(partnerId, email, messageData);
        }
    }
}
