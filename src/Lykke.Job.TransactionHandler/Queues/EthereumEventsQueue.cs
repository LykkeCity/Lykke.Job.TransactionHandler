using System;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Job.TransactionHandler.Services;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.EthereumCore.Contracts.Events;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.EthereumCore;
using Lykke.RabbitMq.Mongo.Deduplicator;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class EthereumEventsQueue : IQueueSubscriber
    {
        private const string QueueName = "lykke.transactionhandler.ethereum.events";
        private const string HotWalletQueueName = "lykke.transactionhandler.ethereum.hotwallet.events";

        private readonly ILog _log;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly AppSettings.TransactionHandlerSettings _settings;
        private readonly AppSettings.EthRabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<CoinEvent> _subscriber;
        private RabbitMqSubscriber<HotWalletEvent> _subscriberHotWallet;

        public EthereumEventsQueue(
            AppSettings.EthRabbitMqSettings config, 
            ILog log,
            ICqrsEngine cqrsEngine,
            AppSettings.TransactionHandlerSettings settings)
        {
            _log = log;
            _rabbitConfig = config;
            _cqrsEngine = cqrsEngine;
            _settings = settings;
        }

        public void Start()
        {
            {
                var settings = new RabbitMqSubscriptionSettings
                {
                    ConnectionString = _rabbitConfig.ConnectionString,
                    QueueName = QueueName,
                    ExchangeName = _rabbitConfig.ExchangeEthereumEvents,
                    DeadLetterExchangeName = $"{_rabbitConfig.ExchangeEthereumEvents}.dlx",
                    RoutingKey = "",
                    IsDurable = true
                };

                try
                {
                    var resilentErrorHandlingStrategyEth = new ResilientErrorHandlingStrategy(_log, settings,
                        retryTimeout: TimeSpan.FromSeconds(10),
                        retryNum: 10,
                        next: new DeadQueueErrorHandlingStrategy(_log, settings)); 
                    _subscriber = new RabbitMqSubscriber<CoinEvent>(settings, resilentErrorHandlingStrategyEth)
                        .SetMessageDeserializer(new JsonMessageDeserializer<CoinEvent>())
                        .SetMessageReadStrategy(new MessageReadQueueStrategy())
                        .SetAlternativeExchange(settings.ConnectionString)
                        .SetDeduplicator(MongoStorageDeduplicator.Create(_settings.MongoDeduplicator.ConnectionString, _settings.MongoDeduplicator.CollectionName))
                        .Subscribe(SendEventToCQRS)
                        .CreateDefaultBinding()
                        .SetLogger(_log)
                        .Start();
                }
                catch (Exception ex)
                {
                    _log.WriteError(nameof(EthereumEventsQueue), nameof(Start), ex);
                    throw;
                }
            }

            #region HotWallet

            {
                string exchangeName = $"{_rabbitConfig.ExchangeEthereumEvents}.hotwallet";
                var settings = new RabbitMqSubscriptionSettings
                {
                    ConnectionString = _rabbitConfig.ConnectionString,
                    QueueName = HotWalletQueueName,
                    ExchangeName = exchangeName,
                    DeadLetterExchangeName = $"{exchangeName}.dlx",
                    RoutingKey = "",
                    IsDurable = true
                };

                var resilentErrorHandlingStrategyEth = new ResilientErrorHandlingStrategy(_log, settings,
                      retryTimeout: TimeSpan.FromSeconds(10),
                      retryNum: 10,
                      next: new DeadQueueErrorHandlingStrategy(_log, settings));

                try
                {
                    _subscriberHotWallet = new RabbitMqSubscriber<HotWalletEvent>(settings,
                         resilentErrorHandlingStrategyEth)
                        .SetMessageDeserializer(new JsonMessageDeserializer<HotWalletEvent>())
                        .SetMessageReadStrategy(new MessageReadQueueStrategy())
                        .SetAlternativeExchange(settings.ConnectionString)
                        .SetDeduplicator(MongoStorageDeduplicator.Create(_settings.MongoDeduplicator.ConnectionString, _settings.MongoDeduplicator.CollectionName))
                        .Subscribe(SendHotWalletEventToCQRS)
                        .CreateDefaultBinding()
                        .SetLogger(_log)
                        .Start();
                }
                catch (Exception ex)
                {
                    _log.WriteError(nameof(EthereumEventsQueue), nameof(Start), ex);
                    throw;
                }
            }

            #endregion

        }

        public void Stop()
        {
            _subscriber?.Stop();
            _subscriberHotWallet?.Stop();
        }

        public Task<bool> SendEventToCQRS(CoinEvent @event)
        {
            _cqrsEngine.SendCommand(new ProcessEthCoinEventCommand
                {
                    Additional = @event.Additional,
                    Amount = @event.Amount,
                    CoinEventType = @event.CoinEventType,
                    ContractAddress = @event.ContractAddress,
                    EventTime = @event.EventTime,
                    FromAddress = @event.FromAddress,
                    OperationId = @event.OperationId,
                    ToAddress = @event.ToAddress,
                    TransactionHash = @event.TransactionHash,
                },
                BoundedContexts.EthereumCommands,
                BoundedContexts.EthereumCommands);

            return Task.FromResult(true);
        }

        public Task<bool> SendHotWalletEventToCQRS(HotWalletEvent @event)
        {
            _cqrsEngine.SendCommand(new ProcessHotWalletErc20EventCommand
                {
                    Amount = @event.Amount,
                    TransactionHash = @event.TransactionHash,
                    ToAddress = @event.ToAddress,
                    OperationId = @event.OperationId,
                    FromAddress = @event.FromAddress,
                    EventTime = @event.EventTime,
                    EventType = @event.EventType,
                    TokenAddress = @event.TokenAddress
                },
                BoundedContexts.EthereumCommands,
                BoundedContexts.EthereumCommands);

            return Task.FromResult(true);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
