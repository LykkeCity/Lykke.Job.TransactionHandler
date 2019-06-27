using System;
using System.Threading.Tasks;
using Autofac;
using Common.Log;
using Lykke.Common.Log;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.EthereumCore.Contracts.Events;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.EthereumCore;
using Lykke.Job.TransactionHandler.Settings;
using Lykke.RabbitMq.Mongo.Deduplicator;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class EthereumEventsQueue : IStartable, IDisposable
    {
        private const string QueueName = "lykke.transactionhandler.ethereum.events";
        private const string HotWalletQueueName = "lykke.transactionhandler.ethereum.hotwallet.events";

        private readonly ILog _log;
        private readonly MongoDeduplicatorSettings _deduplicatorSettings;
        private readonly EthRabbitMqSettings _rabbitMqSettings;
        private readonly ILogFactory _logFactory;
        private readonly ICqrsEngine _cqrsEngine;
        private RabbitMqSubscriber<CoinEvent> _subscriber;
        private RabbitMqSubscriber<HotWalletEvent> _subscriberHotWallet;

        public EthereumEventsQueue(
            MongoDeduplicatorSettings deduplicatorSettings,
            EthRabbitMqSettings rabbitMqSettings,
            ILogFactory logFactory,
            ICqrsEngine cqrsEngine
            )
        {
            _deduplicatorSettings = deduplicatorSettings;
            _rabbitMqSettings = rabbitMqSettings;
            _logFactory = logFactory;
            _log = logFactory.CreateLog(this);
            _cqrsEngine = cqrsEngine;
        }

        public void Start()
        {
            {
                var settings = new RabbitMqSubscriptionSettings
                {
                    ConnectionString = _rabbitMqSettings.ConnectionString,
                    QueueName = QueueName,
                    ExchangeName = _rabbitMqSettings.ExchangeEthereumEvents,
                    DeadLetterExchangeName = $"{_rabbitMqSettings.ExchangeEthereumEvents}.dlx",
                    RoutingKey = "",
                    IsDurable = true
                };

                try
                {
                    var resilentErrorHandlingStrategyEth = new ResilientErrorHandlingStrategy(_logFactory, settings,
                        retryTimeout: TimeSpan.FromSeconds(10),
                        retryNum: 10,
                        next: new DeadQueueErrorHandlingStrategy(_logFactory, settings));

                    _subscriber = new RabbitMqSubscriber<CoinEvent>(_logFactory, settings, resilentErrorHandlingStrategyEth)
                        .SetMessageDeserializer(new JsonMessageDeserializer<CoinEvent>())
                        .SetMessageReadStrategy(new MessageReadQueueStrategy())
                        .SetAlternativeExchange(settings.ConnectionString)
                        .SetDeduplicator(MongoStorageDeduplicator.Create(_deduplicatorSettings.ConnectionString, _deduplicatorSettings.CollectionName))
                        .Subscribe(SendEventToCqrs)
                        .CreateDefaultBinding()
                        .Start();
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                    throw;
                }
            }

            #region HotWallet

            {
                string exchangeName = $"{_rabbitMqSettings.ExchangeEthereumEvents}.hotwallet";
                var settings = new RabbitMqSubscriptionSettings
                {
                    ConnectionString = _rabbitMqSettings.ConnectionString,
                    QueueName = HotWalletQueueName,
                    ExchangeName = exchangeName,
                    DeadLetterExchangeName = $"{exchangeName}.dlx",
                    RoutingKey = "",
                    IsDurable = true
                };

                var resilentErrorHandlingStrategyEth = new ResilientErrorHandlingStrategy(_logFactory, settings,
                      retryTimeout: TimeSpan.FromSeconds(10),
                      retryNum: 10,
                      next: new DeadQueueErrorHandlingStrategy(_logFactory, settings));

                try
                {
                    _subscriberHotWallet = new RabbitMqSubscriber<HotWalletEvent>(_logFactory, settings,
                         resilentErrorHandlingStrategyEth)
                        .SetMessageDeserializer(new JsonMessageDeserializer<HotWalletEvent>())
                        .SetMessageReadStrategy(new MessageReadQueueStrategy())
                        .SetAlternativeExchange(settings.ConnectionString)
                        .SetDeduplicator(MongoStorageDeduplicator.Create(_deduplicatorSettings.ConnectionString, _deduplicatorSettings.CollectionName))
                        .Subscribe(SendHotWalletEventToCqrs)
                        .CreateDefaultBinding()
                        .Start();
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                    throw;
                }
            }

            #endregion

        }

        public void Dispose()
        {
            _subscriber?.Stop();
            _subscriberHotWallet?.Stop();
        }

        private Task<bool> SendEventToCqrs(CoinEvent @event)
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

        private Task<bool> SendHotWalletEventToCqrs(HotWalletEvent @event)
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
    }
}
