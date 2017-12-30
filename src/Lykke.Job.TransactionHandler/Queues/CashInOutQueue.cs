using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Services;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class CashInOutQueue : IQueueSubscriber
    {
        private const string QueueName = "transactions.cashinout";

        private readonly ILog _log;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IBitCoinTransactionsRepository _bitcoinTransactionsRepository;
        private readonly IClientSettingsRepository _clientSettingsRepository;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IDeduplicator _deduplicator;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly IBitcoinCashinRepository _bitcoinCashinTypeRepository;

        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<CashInOutQueueMessage> _subscriber;

        public CashInOutQueue(
            [NotNull] ILog log,
            [NotNull] AppSettings.RabbitMqSettings config,
            [NotNull] ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            [NotNull] IBitCoinTransactionsRepository bitcoinTransactionsRepository,
            [NotNull] IClientSettingsRepository clientSettingsRepository,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IDeduplicator deduplicator, IBitcoinCashinRepository bitcoinCashinTypeRepository,
            [NotNull] ICqrsEngine cqrsEngine)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _rabbitConfig = config ?? throw new ArgumentNullException(nameof(config));
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient ?? throw new ArgumentNullException(nameof(cashOperationsRepositoryClient));
            _bitcoinTransactionsRepository = bitcoinTransactionsRepository ?? throw new ArgumentNullException(nameof(bitcoinTransactionsRepository));
            _clientSettingsRepository = clientSettingsRepository ?? throw new ArgumentNullException(nameof(clientSettingsRepository));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
            _bitcoinCashinTypeRepository = bitcoinCashinTypeRepository;
            _cqrsEngine = cqrsEngine ?? throw new ArgumentNullException(nameof(cqrsEngine));
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.ConnectionString,
                QueueName = QueueName,
                ExchangeName = _rabbitConfig.ExchangeCashOperation,
                DeadLetterExchangeName = $"{_rabbitConfig.ExchangeCashOperation}.dlx",
                RoutingKey = "",
                IsDurable = true
            };

            try
            {
                _subscriber = new RabbitMqSubscriber<CashInOutQueueMessage>(settings, new DeadQueueErrorHandlingStrategy(_log, settings))
                    .SetMessageDeserializer(new JsonMessageDeserializer<CashInOutQueueMessage>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .Subscribe(ProcessMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(CashInOutQueue), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        public async Task ProcessMessage(CashInOutQueueMessage queueMessage)
        {
            if (!await _deduplicator.EnsureNotDuplicateAsync(queueMessage))
            {
                await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(ProcessMessage), queueMessage.ToJson(), "Duplicated message");
                return;
            }

            var transaction = await _bitcoinTransactionsRepository.FindByTransactionIdAsync(queueMessage.Id);
            if (transaction == null)
            {
                // external cashin
                if (_cashOperationsRepositoryClient.GetAsync(queueMessage.ClientId, queueMessage.Id) != null)
                {
                    var asset = await _assetsServiceWithCache.TryGetAssetAsync(queueMessage.AssetId);

                    if (!await _clientSettingsRepository.IsOffchainClient(queueMessage.ClientId) || asset.Blockchain != Blockchain.Bitcoin || asset.IsTrusted && asset.Id != LykkeConstants.BitcoinAssetId)
                        return;

                    if (asset.Id == LykkeConstants.BitcoinAssetId)
                    {
                        var createOffchainRequestCommand = new CreateOffchainCashoutRequestCommand
                        {
                            Id = Guid.NewGuid().ToString(),
                            ClientId = queueMessage.ClientId,
                            AssetId = queueMessage.AssetId,
                            Amount = (decimal)queueMessage.Amount.ParseAnyDouble()
                        };

                        _cqrsEngine.SendCommand(createOffchainRequestCommand, "tx-handler", "bitcoin");
                    }
                }
                else
                {
                    await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(ProcessMessage), queueMessage.ToJson(), "unknown transaction");
                }
            }
            else
            {
                switch (transaction.CommandType)
                {
                    case BitCoinCommands.CashIn:
                    case BitCoinCommands.Issue:
                        var issueCommand = new Commands.IssueCommand { TransactionId = transaction.TransactionId, Message = queueMessage };
                        _cqrsEngine.SendCommand(issueCommand, "tx-handler", "tx-handler");
                        break;
                    case BitCoinCommands.CashOut:
                        var cashoutCommand = new Commands.CashoutCommand { TransactionId = transaction.TransactionId, BlockchainHash = transaction.BlockchainHash, Message = queueMessage };
                        _cqrsEngine.SendCommand(cashoutCommand, "tx-handler", "tx-handler");
                        break;
                    case BitCoinCommands.Destroy:
                        var destroyCommand = new Commands.DestroyCommand { TransactionId = transaction.TransactionId, Message = queueMessage };
                        _cqrsEngine.SendCommand(destroyCommand, "tx-handler", "tx-handler");
                        break;
                    case BitCoinCommands.ManualUpdate:
                        var context = transaction.GetContextData<CashOutContextData>();
                        var manualUpdateCommand = new Commands.ManualUpdateCommand { AddressTo = context.Address, BlockchainHash = transaction.BlockchainHash, Message = queueMessage };
                        _cqrsEngine.SendCommand(manualUpdateCommand, "tx-handler", "tx-handler");
                        break;
                    default:
                        await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(ProcessMessage), queueMessage.ToJson(), $"Unknown command type (value = [{transaction.CommandType}])");
                        break;
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

}