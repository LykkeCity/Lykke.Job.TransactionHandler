using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.LimitTrades;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Services;
using Lykke.RabbitMq.Mongo.Deduplicator;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class LimitTradeQueue : IQueueSubscriber
    {
        private const string QueueName = "transactions.limit-trades";
        private const bool QueueDurable = true;

        private readonly ILog _log;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly AppSettings.TransactionHandlerSettings _settings;

        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<LimitQueueItem> _subscriber;

        public LimitTradeQueue(
            AppSettings.RabbitMqSettings config,
            ILog log,
            ICqrsEngine cqrsEngine,
            AppSettings.TransactionHandlerSettings settings
            )
        {
            _rabbitConfig = config;
            _log = log;
            _cqrsEngine = cqrsEngine;
            _settings = settings;
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.ConnectionString,
                QueueName = QueueName,
                ExchangeName = _rabbitConfig.ExchangeLimit,
                DeadLetterExchangeName = $"{_rabbitConfig.ExchangeLimit}.dlx",
                RoutingKey = "",
                IsDurable = QueueDurable
            };

            try
            {
                _subscriber = new RabbitMqSubscriber<LimitQueueItem>(settings, new DeadQueueErrorHandlingStrategy(_log, settings))
                    .SetMessageDeserializer(new JsonMessageDeserializer<LimitQueueItem>())
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
                _log.WriteErrorAsync(nameof(LimitTradeQueue), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        private async Task ProcessMessage(LimitQueueItem tradeItem)
        {
            await _log.WriteInfoAsync(nameof(LimitTradeQueue), nameof(ProcessMessage), tradeItem.ToJson());

            foreach (var limitOrderWithTrades in tradeItem.Orders)
            {
                var command = new ProcessLimitOrderCommand
                {
                    LimitOrder = limitOrderWithTrades
                };

                _cqrsEngine.SendCommand(command, BoundedContexts.TxHandler, BoundedContexts.TxHandler);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
