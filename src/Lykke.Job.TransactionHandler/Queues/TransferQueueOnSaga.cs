using System;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class TransferQueueOnSaga : IQueueSubscriber
    {
#if DEBUG
        private const string QueueName = "transactions.transfer-dev";
        private const bool QueueDurable = false;
#else
        private const string QueueName = "transactions.transfer";
        private const bool QueueDurable = true;
#endif

        private readonly ILog _log;
        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<TransferQueueMessage> _subscriber;
        private readonly ICqrsEngine _cqrsEngine;


        public TransferQueueOnSaga(
            [NotNull] AppSettings.RabbitMqSettings config,
            [NotNull] ILog log,
            [NotNull] ICqrsEngine cqrsEngine)
        {
            _rabbitConfig = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _cqrsEngine = cqrsEngine ?? throw new ArgumentNullException(nameof(cqrsEngine));
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.ConnectionString,
                QueueName = QueueName,
                ExchangeName = _rabbitConfig.ExchangeTransfer,
                DeadLetterExchangeName = $"{_rabbitConfig.ExchangeTransfer}.dlx",
                RoutingKey = "",
                IsDurable = QueueDurable
            };

            try
            {
                _subscriber = new RabbitMqSubscriber<TransferQueueMessage>(
                        settings,
                        new ResilientErrorHandlingStrategy(_log, settings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_log, settings)))
                    .SetMessageDeserializer(new JsonMessageDeserializer<TransferQueueMessage>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .Subscribe(ProcessMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(TransferQueueOnSaga), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        private async Task ProcessMessage(TransferQueueMessage queueMessage)
        {
            _cqrsEngine.SendCommand(new Commands.SaveTransferOperationStateCommand
            {
                QueueMessage = queueMessage
            }, BoundedContexts.Self, BoundedContexts.Operations);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}