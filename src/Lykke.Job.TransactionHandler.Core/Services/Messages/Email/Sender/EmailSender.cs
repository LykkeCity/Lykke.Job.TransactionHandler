using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.ContentGenerator.MessagesData;

namespace Lykke.Job.TransactionHandler.Core.Services.Messages.Email.Sender
{
    public interface IEmailSender
    {
        Task SendEmailAsync<T>(string partnerId, string emailAddress, T messageData) where T : IEmailMessageData;
    }
}