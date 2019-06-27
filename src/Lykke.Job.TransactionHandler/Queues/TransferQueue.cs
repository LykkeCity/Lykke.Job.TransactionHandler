using System;
using System.Threading.Tasks;
using Autofac;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Settings;
using Lykke.MatchingEngine.Connector.Models.Events;
using Lykke.MatchingEngine.Connector.Models.Events.Common;
using Lykke.RabbitMq.Mongo.Deduplicator;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class TransferQueue : IStartable, IDisposable
    {
        private const bool QueueDurable = true;

        private readonly ILog _log;
        private readonly MongoDeduplicatorSettings _deduplicatorSettings;
        private readonly RabbitMqSettings _rabbitMqSettings;
        private readonly ILogFactory _logFactory;
        private readonly ICqrsEngine _cqrsEngine;

        private RabbitMqSubscriber<CashTransferEvent> _subscriber;

        public TransferQueue(
            MongoDeduplicatorSettings deduplicatorSettings,
            RabbitMqSettings rabbitMqSettings,
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
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitMqSettings.NewMeRabbitConnString,
                QueueName = $"{_rabbitMqSettings.EventsExchange}.transfers.txhandler",
                ExchangeName = _rabbitMqSettings.EventsExchange,
                RoutingKey = ((int)MessageType.CashTransfer).ToString(),
                IsDurable = QueueDurable
            };
            settings.DeadLetterExchangeName = $"{settings.QueueName}.dlx";

            try
            {
                _subscriber = new RabbitMqSubscriber<CashTransferEvent>(_logFactory,
                        settings,
                        new ResilientErrorHandlingStrategy(_logFactory, settings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_logFactory, settings)))
                    .SetMessageDeserializer(new ProtobufMessageDeserializer<CashTransferEvent>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .SetAlternativeExchange(_rabbitMqSettings.AlternateConnectionString)
                    .SetDeduplicator(MongoStorageDeduplicator.Create(_deduplicatorSettings.ConnectionString, _deduplicatorSettings.CollectionName))
                    .Subscribe(ProcessMessage)
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
            _subscriber?.Stop();
        }

        private Task ProcessMessage(CashTransferEvent transfer)
        {
            var cashTransfer = ToOldModel(transfer);

            _cqrsEngine.SendCommand(
                new Commands.SaveTransferOperationStateCommand { QueueMessage = cashTransfer },
                BoundedContexts.TxHandler,
                BoundedContexts.Operations);

            return Task.CompletedTask;
        }

        private TransferQueueMessage ToOldModel(CashTransferEvent transferEvent)
        {
            return new TransferQueueMessage
            {
                Id = transferEvent.Header.MessageId,
                AssetId = transferEvent.CashTransfer.AssetId,
                Date = transferEvent.Header.Timestamp,
                FromClientId = transferEvent.CashTransfer.FromWalletId,
                ToClientid = transferEvent.CashTransfer.ToWalletId,
                Amount = transferEvent.CashTransfer.Volume,
                Fees = transferEvent.CashTransfer.Fees?.ToOldFees(transferEvent.Header.Timestamp),
            };
        }
    }
}
