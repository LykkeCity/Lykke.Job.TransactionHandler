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
using Lykke.RabbitMq.Mongo.Deduplicator;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class EthereumEventsQueue : IStartable, IDisposable
    {
        private const string QueueName = "lykke.transactionhandler.ethereum.events";
        private const string HotWalletQueueName = "lykke.transactionhandler.ethereum.hotwallet.events";

        private readonly ILog _log;
        private readonly string _mongoConnectionString;
        private readonly string _mongoCollectionName;
        private readonly string _connectionString;
        private readonly string _exchangeEthereumEvents;
        private readonly ILogFactory _logFactory;
        private readonly ICqrsEngine _cqrsEngine;
        private RabbitMqSubscriber<CoinEvent> _subscriber;
        private RabbitMqSubscriber<HotWalletEvent> _subscriberHotWallet;

        public EthereumEventsQueue(
            string mongoConnectionString,
            string mongoCollectionName,
            string connectionString,
            string exchangeEthereumEvents,
            ILogFactory logFactory,
            ICqrsEngine cqrsEngine
            )
        {
            _mongoConnectionString = mongoConnectionString;
            _mongoCollectionName = mongoCollectionName;
            _connectionString = connectionString;
            _exchangeEthereumEvents = exchangeEthereumEvents;
            _logFactory = logFactory;
            _log = logFactory.CreateLog(this);
            _cqrsEngine = cqrsEngine;
        }

        public void Start()
        {
            {
                var settings = new RabbitMqSubscriptionSettings
                {
                    ConnectionString = _connectionString,
                    QueueName = QueueName,
                    ExchangeName = _exchangeEthereumEvents,
                    DeadLetterExchangeName = $"{_exchangeEthereumEvents}.dlx",
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
                        .SetDeduplicator(MongoStorageDeduplicator.Create(_mongoConnectionString, _mongoCollectionName))
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
                string exchangeName = $"{_exchangeEthereumEvents}.hotwallet";
                var settings = new RabbitMqSubscriptionSettings
                {
                    ConnectionString = _connectionString,
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
                        .SetDeduplicator(MongoStorageDeduplicator.Create(_mongoConnectionString, _mongoCollectionName))
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
