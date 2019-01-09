﻿using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;
using Lykke.RabbitMq.Mongo.Deduplicator;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class TradeQueue : IQueueSubscriber
    {
        private const string QueueName = "transactions.trades";
        private const bool QueueDurable = true;

        private readonly ILog _log;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly AppSettings.TransactionHandlerSettings _settings;

        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<TradeQueueItem> _subscriber;

        public TradeQueue(
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
                ExchangeName = _rabbitConfig.ExchangeSwap,
                DeadLetterExchangeName = $"{_rabbitConfig.ExchangeSwap}.dlx",
                RoutingKey = "",
                IsDurable = QueueDurable
            };

            try
            {
                _subscriber = new RabbitMqSubscriber<TradeQueueItem>(
                        settings,
                        new ResilientErrorHandlingStrategy(_log, settings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_log, settings)))
                    .SetMessageDeserializer(new JsonMessageDeserializer<TradeQueueItem>())
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
                _log.WriteError(nameof(TradeQueue), nameof(Start), ex);
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        private Task ProcessMessage(TradeQueueItem queueMessage)
        {
            _log.WriteInfo(nameof(TradeQueue), nameof(ProcessMessage), queueMessage.ToJson());

            _cqrsEngine.SendCommand(new Commands.CreateTradeCommand
            {
                QueueMessage = queueMessage
            }, BoundedContexts.TxHandler, BoundedContexts.Trades);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
