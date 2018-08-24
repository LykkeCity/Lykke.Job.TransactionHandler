using System.Threading.Tasks;
using AzureStorage.Queue;
using Lykke.Job.TransactionHandler.Core.Domain.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email.ContentGenerator.MessagesData;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Messages.Email
{
    public class EmailCommandProducer : IEmailCommandProducer
    {
        private readonly IQueueExt _queueExt;

        public EmailCommandProducer(IQueueExt queueExt)
        {
            _queueExt = queueExt;

            _queueExt.RegisterTypes(
                QueueType.Create(new NoRefundDepositDoneData().MessageId(), typeof(QueueRequestModel<SendEmailData<NoRefundDepositDoneData>>))
            );
            _queueExt.RegisterTypes(
                QueueType.Create(new NoRefundOCashOutData().MessageId(), typeof(QueueRequestModel<SendEmailData<NoRefundOCashOutData>>))
            );
            _queueExt.RegisterTypes(
                QueueType.Create(new TransferCompletedData().MessageId(), typeof(QueueRequestModel<SendEmailData<TransferCompletedData>>))
            );
            _queueExt.RegisterTypes(
                QueueType.Create(new DirectTransferCompletedData().MessageId(), typeof(QueueRequestModel<SendEmailData<DirectTransferCompletedData>>))
            );
        }

        public Task ProduceSendEmailCommand<T>(string partnerId, string mailAddress, T msgData)
        {
            var data = SendEmailData<T>.Create(partnerId, mailAddress, msgData);
            var msg = new QueueRequestModel<SendEmailData<T>> { Data = data };
            return _queueExt.PutMessageAsync(msg);
        }
    }
}