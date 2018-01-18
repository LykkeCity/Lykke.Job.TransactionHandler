using System;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class TradeQueue : IQueueSubscriber
    {
#if DEBUG
        private const string QueueName = "transactions.trades-dev";
        private const bool QueueDurable = false;
#else
        private const string QueueName = "transactions.trades";
        private const bool QueueDurable = true;
#endif

        private readonly ILog _log;
        private readonly ICqrsEngine _cqrsEngine;

        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<TradeQueueItem> _subscriber;

        public TradeQueue(
            AppSettings.RabbitMqSettings config,
            ILog log,
            ICqrsEngine cqrsEngine)
        {
            _rabbitConfig = config;
            _log = log;
            _cqrsEngine = cqrsEngine;
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
                    .Subscribe(ProcessMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(TradeQueue), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        private async Task ProcessMessage(TradeQueueItem queueMessage)
        {
            _cqrsEngine.SendCommand(new Commands.CreateTradeCommand
            {
                QueueMessage = queueMessage
            }, "TradeQueue", BoundedContexts.Trades);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}