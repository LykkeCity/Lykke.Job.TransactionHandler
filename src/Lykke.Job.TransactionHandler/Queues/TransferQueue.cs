using System;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.MatchingEngine.Connector.Models.Events;
using Lykke.MatchingEngine.Connector.Models.Events.Common;
using Lykke.RabbitMq.Mongo.Deduplicator;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class TransferQueue : IQueueSubscriber
    {
        private const bool QueueDurable = true;

        private readonly ILog _log;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private readonly AppSettings.TransactionHandlerSettings _settings;

        private RabbitMqSubscriber<CashTransferEvent> _subscriber;

        public TransferQueue(
            [NotNull] AppSettings.RabbitMqSettings config,
            [NotNull] ILog log,
            [NotNull] ICqrsEngine cqrsEngine,
            [NotNull] AppSettings.TransactionHandlerSettings settings
            )
        {
            _rabbitConfig = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _cqrsEngine = cqrsEngine ?? throw new ArgumentNullException(nameof(cqrsEngine));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.ConnectionString,
                QueueName = $"{_rabbitConfig.EventsExchange}.transfers.txhandler",
                ExchangeName = _rabbitConfig.EventsExchange,
                RoutingKey = ((int)MessageType.CashTransfer).ToString(),
                IsDurable = QueueDurable
            };
            settings.DeadLetterExchangeName = $"{settings.QueueName}.dlx";

            try
            {
                _subscriber = new RabbitMqSubscriber<CashTransferEvent>(
                        settings,
                        new ResilientErrorHandlingStrategy(_log, settings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_log, settings)))
                    .SetMessageDeserializer(new ProtobufMessageDeserializer<CashTransferEvent>())
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
                _log.WriteError(nameof(TransferQueue), nameof(Start), ex);
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        public void Dispose()
        {
            Stop();
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
