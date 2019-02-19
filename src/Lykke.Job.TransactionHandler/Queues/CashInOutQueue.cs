using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Sagas;
using Lykke.Job.TransactionHandler.Services;
using Lykke.MatchingEngine.Connector.Models.Events;
using Lykke.MatchingEngine.Connector.Models.Events.Common;
using Lykke.RabbitMq.Mongo.Deduplicator;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class CashInOutQueue : IQueueSubscriber
    {
        private const string OldQueueName = "transactions.cashinout";
        private const bool QueueDurable = true;

        private readonly ILog _log;
        private readonly CashInOutMessageProcessor _messageProcessor;
        private readonly AppSettings.TransactionHandlerSettings _settings;
        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private readonly ConcurrentDictionary<string, bool> _alreadyProcessed = new ConcurrentDictionary<string, bool>();

        private RabbitMqSubscriber<CashInOutQueueMessage> _oldSubscriber;
        private RabbitMqSubscriber<CashInEvent> _cashinSubscriber;
        private RabbitMqSubscriber<CashOutEvent> _cashoutSubscriber;

        public CashInOutQueue(
            [NotNull] ILog log,
            [NotNull] AppSettings.RabbitMqSettings config,
            [NotNull] CashInOutMessageProcessor messageProcessor,
            [NotNull] AppSettings.TransactionHandlerSettings settings)
        {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _rabbitConfig = config ?? throw new ArgumentNullException(nameof(config));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Start()
        {
            var oldSettings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.ConnectionString,
                QueueName = OldQueueName,
                ExchangeName = _rabbitConfig.ExchangeCashOperation,
                DeadLetterExchangeName = $"{_rabbitConfig.ExchangeCashOperation}.dlx",
                RoutingKey = "",
                IsDurable = QueueDurable
            };

            var cashinSettings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.NewMeRabbitConnString,
                QueueName = $"{_rabbitConfig.EventsExchange}.cashin.txhandler",
                ExchangeName = _rabbitConfig.EventsExchange,
                RoutingKey = ((int)MessageType.CashIn).ToString(),
                IsDurable = QueueDurable
            };
            cashinSettings.DeadLetterExchangeName = $"{cashinSettings.QueueName}.dlx";

            var cashoutSettings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.NewMeRabbitConnString,
                QueueName = $"{_rabbitConfig.EventsExchange}.cashout.txhandler",
                ExchangeName = _rabbitConfig.EventsExchange,
                RoutingKey = ((int)MessageType.CashOut).ToString(),
                IsDurable = QueueDurable
            };
            cashoutSettings.DeadLetterExchangeName = $"{cashoutSettings.QueueName}.dlx";

            try
            {
                _oldSubscriber = new RabbitMqSubscriber<CashInOutQueueMessage>(
                        oldSettings,
                        new ResilientErrorHandlingStrategy(_log, oldSettings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_log, oldSettings)))
                    .SetMessageDeserializer(new JsonMessageDeserializer<CashInOutQueueMessage>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .Subscribe(ProcessOldMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();

                _cashinSubscriber = new RabbitMqSubscriber<CashInEvent>(
                        cashinSettings,
                        new ResilientErrorHandlingStrategy(_log, cashinSettings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_log, cashinSettings)))
                    .SetMessageDeserializer(new ProtobufMessageDeserializer<CashInEvent>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .SetAlternativeExchange(_rabbitConfig.AlternateConnectionString)
                    .SetDeduplicator(MongoStorageDeduplicator.Create(_settings.MongoDeduplicator.ConnectionString, _settings.MongoDeduplicator.CollectionName))
                    .Subscribe(ProcessCashinMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();

                _cashoutSubscriber = new RabbitMqSubscriber<CashOutEvent>(
                        cashoutSettings,
                        new ResilientErrorHandlingStrategy(_log, cashoutSettings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_log, cashoutSettings)))
                    .SetMessageDeserializer(new ProtobufMessageDeserializer<CashOutEvent>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .SetAlternativeExchange(_rabbitConfig.AlternateConnectionString)
                    .SetDeduplicator(MongoStorageDeduplicator.Create(_settings.MongoDeduplicator.ConnectionString, _settings.MongoDeduplicator.CollectionName))
                    .Subscribe(ProcessCashoutMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteError(nameof(CashInOutQueue), nameof(Start), ex);
                throw;
            }
        }

        public void Stop()
        {
            _oldSubscriber?.Stop();
            _cashinSubscriber?.Stop();
            _cashoutSubscriber?.Stop();
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task ProcessOldMessage(CashInOutQueueMessage message)
        {
            if (!_alreadyProcessed.TryAdd(message.Id, true))
            {
                _alreadyProcessed.TryRemove(message.Id, out _);
                return;
            }

            await _messageProcessor.ProcessMessage(message);
        }

        private async Task ProcessCashinMessage(CashInEvent cashinEvent)
        {
            if (!_alreadyProcessed.TryAdd(cashinEvent.Header.MessageId, true))
            {
                _alreadyProcessed.TryRemove(cashinEvent.Header.MessageId, out _);
                return;
            }

            var message = ToOld(cashinEvent);
            await _messageProcessor.ProcessMessage(message);
        }

        private async Task ProcessCashoutMessage(CashOutEvent cashoutEvent)
        {
            if (!_alreadyProcessed.TryAdd(cashoutEvent.Header.MessageId, true))
            {
                _alreadyProcessed.TryRemove(cashoutEvent.Header.MessageId, out _);
                return;
            }

            var message = ToOld(cashoutEvent);
            await _messageProcessor.ProcessMessage(message);
        }

        private CashInOutQueueMessage ToOld(CashInEvent cashInEvent)
        {
            return new CashInOutQueueMessage
            {
                Id = cashInEvent.Header.MessageId,
                ClientId = cashInEvent.CashIn.WalletId,
                Amount = cashInEvent.CashIn.Volume,
                AssetId = cashInEvent.CashIn.AssetId,
                Date = cashInEvent.Header.Timestamp,
                Fees = cashInEvent.CashIn.Fees?.ToOldFees(cashInEvent.Header.Timestamp),
            };
        }

        private CashInOutQueueMessage ToOld(CashOutEvent cashOutEvent)
        {
            return new CashInOutQueueMessage
            {
                Id = cashOutEvent.Header.MessageId,
                ClientId = cashOutEvent.CashOut.WalletId,
                Amount = cashOutEvent.CashOut.Volume.StartsWith('-') ? cashOutEvent.CashOut.Volume : $"-{cashOutEvent.CashOut.Volume}",
                AssetId = cashOutEvent.CashOut.AssetId,
                Date = cashOutEvent.Header.Timestamp,
                Fees = cashOutEvent.CashOut.Fees?.ToOldFees(cashOutEvent.Header.Timestamp),
            };
        }
    }
}

