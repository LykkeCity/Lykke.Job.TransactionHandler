using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Services;
using Lykke.Job.TransactionHandler.Services;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.EthereumCore.Contracts.Events;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.EthereumCore;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class EthereumEventsQueue : IQueueSubscriber
    {
        private const string QueueName = "lykke.transactionhandler.ethereum.events";
        private const string HotWalletQueueName = "lykke.transactionhandler.ethereum.hotwallet.events";

        private readonly ILog _log;
        private readonly IDeduplicator _deduplicator;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly AppSettings.EthRabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<HotWalletEvent> _subscriberHotWallet;

        public EthereumEventsQueue(AppSettings.EthRabbitMqSettings config, ILog log,
            [NotNull] IDeduplicator deduplicator,
            ICqrsEngine cqrsEngine)
        {
            _log = log;
            _rabbitConfig = config;
            _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
            _cqrsEngine = cqrsEngine;
        }

        public void Start()
        {
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
                        .Subscribe(SendHotWalletEventToCQRS)
                        .CreateDefaultBinding()
                        .SetLogger(_log)
                        .Start();
                }
                catch (Exception ex)
                {
                    _log.WriteErrorAsync(nameof(EthereumEventsQueue), nameof(Start), null, ex).Wait();
                    throw;
                }
            }

            #endregion

        }

        public void Stop()
        {
            _subscriberHotWallet?.Stop();
        }

        public async Task<bool> SendHotWalletEventToCQRS(HotWalletEvent @event)
        {
            if (!await _deduplicator.EnsureNotDuplicateAsync(@event))
            {
                await _log.WriteWarningAsync(nameof(EthereumEventsQueue), nameof(SendHotWalletEventToCQRS), @event.ToJson(), "Duplicated message");
                return false;
            }

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

            return true;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
