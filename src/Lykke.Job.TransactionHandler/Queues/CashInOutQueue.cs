using System;
using System.Threading.Tasks;
using Autofac;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Sagas;
using Lykke.MatchingEngine.Connector.Models.Events;
using Lykke.MatchingEngine.Connector.Models.Events.Common;
using Lykke.RabbitMq.Mongo.Deduplicator;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class CashInOutQueue : IStartable, IDisposable
    {
        private const bool QueueDurable = true;

        private readonly ILog _log;
        private readonly string _mongoConnectionString;
        private readonly string _mongoCollectionName;
        private readonly string _alternateConnectionString;
        private readonly string _newMeRabbitConnString;
        private readonly string _eventsExchange;
        private readonly ILogFactory _logFactory;
        private readonly CashInOutMessageProcessor _messageProcessor;

        private RabbitMqSubscriber<CashInEvent> _cashinSubscriber;
        private RabbitMqSubscriber<CashOutEvent> _cashoutSubscriber;

        public CashInOutQueue(
            string mongoConnectionString,
            string mongoCollectionName,
            string alternateConnectionString,
            string newMeRabbitConnString,
            string eventsExchange,
            ILogFactory logFactory,
            CashInOutMessageProcessor messageProcessor)
        {
            _mongoConnectionString = mongoConnectionString;
            _mongoCollectionName = mongoCollectionName;
            _alternateConnectionString = alternateConnectionString;
            _newMeRabbitConnString = newMeRabbitConnString;
            _eventsExchange = eventsExchange;
            _logFactory = logFactory;
            _messageProcessor = messageProcessor;
            _log = logFactory.CreateLog(this);
        }

        public void Start()
        {
            var cashinSettings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _newMeRabbitConnString,
                QueueName = $"{_eventsExchange}.cashin.txhandler",
                ExchangeName = _eventsExchange,
                RoutingKey = ((int)MessageType.CashIn).ToString(),
                IsDurable = QueueDurable
            };

            cashinSettings.DeadLetterExchangeName = $"{cashinSettings.QueueName}.dlx";

            var cashoutSettings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _newMeRabbitConnString,
                QueueName = $"{_eventsExchange}.cashout.txhandler",
                ExchangeName = _eventsExchange,
                RoutingKey = ((int)MessageType.CashOut).ToString(),
                IsDurable = QueueDurable
            };

            cashoutSettings.DeadLetterExchangeName = $"{cashoutSettings.QueueName}.dlx";

            try
            {
                _cashinSubscriber = new RabbitMqSubscriber<CashInEvent>(_logFactory,
                        cashinSettings,
                        new ResilientErrorHandlingStrategy(_logFactory, cashinSettings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_logFactory, cashinSettings)))
                    .SetMessageDeserializer(new ProtobufMessageDeserializer<CashInEvent>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .SetAlternativeExchange(_alternateConnectionString)
                    .SetDeduplicator(MongoStorageDeduplicator.Create(_mongoConnectionString, _mongoCollectionName))
                    .Subscribe(ProcessCashinMessage)
                    .CreateDefaultBinding()
                    .Start();

                _cashoutSubscriber = new RabbitMqSubscriber<CashOutEvent>(_logFactory,
                        cashoutSettings,
                        new ResilientErrorHandlingStrategy(_logFactory, cashoutSettings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_logFactory, cashoutSettings)))
                    .SetMessageDeserializer(new ProtobufMessageDeserializer<CashOutEvent>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .SetAlternativeExchange(_alternateConnectionString)
                    .SetDeduplicator(MongoStorageDeduplicator.Create(_mongoConnectionString, _mongoCollectionName))
                    .Subscribe(ProcessCashoutMessage)
                    .CreateDefaultBinding()
                    .Start();
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                throw;
            }
        }

        public void Dispose()
        {
            _cashinSubscriber?.Stop();
            _cashoutSubscriber?.Stop();
        }

        private async Task ProcessCashinMessage(CashInEvent cashinEvent)
        {
            var message = ToOld(cashinEvent);
            await _messageProcessor.ProcessMessage(message);
        }

        private async Task ProcessCashoutMessage(CashOutEvent cashoutEvent)
        {
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
