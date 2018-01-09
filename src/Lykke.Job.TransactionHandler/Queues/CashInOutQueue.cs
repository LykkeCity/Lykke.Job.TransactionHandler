using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Services;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Sagas;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class CashInOutQueue : IQueueSubscriber
    {
        private const string QueueName = "transactions.cashinout";

        private readonly ILog _log;
        private readonly IDeduplicator _deduplicator;
        private readonly CashInOutMessageProcessor _messageProcessor;

        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<CashInOutQueueMessage> _subscriber;

        public CashInOutQueue(
            [NotNull] ILog log,
            [NotNull] AppSettings.RabbitMqSettings config,
            [NotNull] IDeduplicator deduplicator,
            [NotNull] CashInOutMessageProcessor messageProcessor)
        {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _rabbitConfig = config ?? throw new ArgumentNullException(nameof(config));
            _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.ConnectionString,
                QueueName = QueueName,
                ExchangeName = _rabbitConfig.ExchangeCashOperation,
                DeadLetterExchangeName = $"{_rabbitConfig.ExchangeCashOperation}.dlx",
                RoutingKey = "",
                IsDurable = true
            };

            try
            {
                _subscriber = new RabbitMqSubscriber<CashInOutQueueMessage>(
                        settings,
                        new ResilientErrorHandlingStrategy(_log, settings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_log, settings)))
                    .SetMessageDeserializer(new JsonMessageDeserializer<CashInOutQueueMessage>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .Subscribe(ProcessMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(CashInOutQueue), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        private async Task ProcessMessage(CashInOutQueueMessage message)
        {
            if (!await _deduplicator.EnsureNotDuplicateAsync(message))
            {
                await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(ProcessMessage), message.ToJson(), "Duplicated message");
                return;
            }

            await _messageProcessor.ProcessMessage(message);
        }

        public void Dispose()
        {
            Stop();
        }
    }

}