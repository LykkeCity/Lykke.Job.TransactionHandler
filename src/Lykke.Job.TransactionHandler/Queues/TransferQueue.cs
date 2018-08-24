using System;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;
using Lykke.RabbitMq.Mongo.Deduplicator;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class TransferQueue : IQueueSubscriber
    {
        private const string QueueName = "transactions.transfer";
        private const bool QueueDurable = true;

        private readonly ILog _log;
        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<TransferQueueMessage> _subscriber;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly AppSettings.TransactionHandlerSettings _settings;


        public TransferQueue(
            [NotNull] AppSettings.RabbitMqSettings config,
            [NotNull] ILog log,
            [NotNull] ICqrsEngine cqrsEngine,
            [NotNull] AppSettings.TransactionHandlerSettings settings
            )
        {
            _rabbitConfig = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _cqrsEngine = cqrsEngine ?? throw new ArgumentNullException(nameof(cqrsEngine));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
                    .SetAlternativeExchange(_rabbitConfig.AlternateConnectionString)
                    .SetDeduplicator(MongoStorageDeduplicator.Create(_settings.MongoDeduplicator.ConnectionString, _settings.MongoDeduplicator.CollectionName))
                    .Subscribe(ProcessMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(TransferQueue), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        private Task ProcessMessage(TransferQueueMessage queueMessage)
        {
            _cqrsEngine.SendCommand(new Commands.SaveTransferOperationStateCommand
            {
                QueueMessage = queueMessage
            }, BoundedContexts.TxHandler, BoundedContexts.Operations);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
