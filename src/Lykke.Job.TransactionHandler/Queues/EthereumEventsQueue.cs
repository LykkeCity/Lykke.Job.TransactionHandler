using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Services;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Services.Ethereum;
using Lykke.MatchingEngine.Connector.Abstractions.Models;
using Lykke.MatchingEngine.Connector.Abstractions.Services;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.ExchangeOperations.Client;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Lykke.Job.EthereumCore.Contracts.Events;
using Lykke.Job.EthereumCore.Contracts.Enums;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.EthereumCore;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class EthereumEventsQueue : IQueueSubscriber
    {
        private const string QueueName = "lykke.transactionhandler.ethereum.events";
        private const string HotWalletQueueName = "lykke.transactionhandler.ethereum.hotwallet.events";

        private readonly ILog _log;
        private readonly IMatchingEngineClient _matchingEngineClient;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly IPaymentTransactionsRepository _paymentTransactionsRepository;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly ITradeOperationsRepositoryClient _clientTradesRepositoryClient;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly ITransferOperationsRepositoryClient _transferEventsRepositoryClient;
        private readonly ITransactionService _transactionService;
        private readonly IAssetsService _assetsService;
        private readonly IEthererumPendingActionsRepository _ethererumPendingActionsRepository;
        private readonly IDeduplicator _deduplicator;
        private readonly IExchangeOperationsServiceClient _exchangeOperationsServiceClient;
        private readonly IClientCommentsRepository _clientCommentsRepository;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<CoinEvent> _subscriber;
        private RabbitMqSubscriber<Lykke.Job.EthereumCore.Contracts.Events.HotWalletEvent> _subscriberHotWallet;

        public EthereumEventsQueue(AppSettings.RabbitMqSettings config, ILog log,
            IMatchingEngineClient matchingEngineClient,
            ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            IClientAccountClient clientAccountClient,
            ISrvEmailsFacade srvEmailsFacade,
            IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            IPaymentTransactionsRepository paymentTransactionsRepository,
            IWalletCredentialsRepository walletCredentialsRepository,
            ITradeOperationsRepositoryClient clientTradesRepositoryClient,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            ITransferOperationsRepositoryClient transferEventsRepositoryClient,
            IAssetsServiceWithCache assetsServiceWithCache,
            ITransactionService transactionService,
            IAssetsService assetsService,
            IEthererumPendingActionsRepository ethererumPendingActionsRepository,
            IExchangeOperationsServiceClient exchangeOperationsServiceClient,
            IClientCommentsRepository clientCommentsRepository,
            [NotNull] IDeduplicator deduplicator,
            ICqrsEngine cqrsEngine)
        {
            _log = log;
            _matchingEngineClient = matchingEngineClient;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
            _clientAccountClient = clientAccountClient;
            _srvEmailsFacade = srvEmailsFacade;
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository;
            _paymentTransactionsRepository = paymentTransactionsRepository;
            _walletCredentialsRepository = walletCredentialsRepository;
            _clientTradesRepositoryClient = clientTradesRepositoryClient;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _assetsServiceWithCache = assetsServiceWithCache;
            _rabbitConfig = config;
            _transferEventsRepositoryClient = transferEventsRepositoryClient;
            _transactionService = transactionService;
            _assetsService = assetsService;
            _ethererumPendingActionsRepository = ethererumPendingActionsRepository;
            _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
            _exchangeOperationsServiceClient = exchangeOperationsServiceClient;
            _clientCommentsRepository = clientCommentsRepository;
            _cqrsEngine = cqrsEngine;
        }

        public void Start()
        {
            {
                var settings = new RabbitMqSubscriptionSettings
                {
                    ConnectionString = _rabbitConfig.ConnectionString,
                    QueueName = QueueName,
                    ExchangeName = _rabbitConfig.ExchangeEthereumCashIn,
                    DeadLetterExchangeName = $"{_rabbitConfig.ExchangeEthereumCashIn}.dlx",
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
                        .Subscribe(SendEventToCQRS)
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

            #region HotWallet

            {
                string exchangeName = $"{_rabbitConfig.ExchangeEthereumCashIn}.hotwallet";
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
            _subscriber?.Stop();
            _subscriberHotWallet?.Stop();
        }

        public async Task<bool> SendEventToCQRS(CoinEvent @event)
        {
            if (!await _deduplicator.EnsureNotDuplicateAsync(@event))
            {
                await _log.WriteWarningAsync(nameof(EthereumEventsQueue), nameof(SendEventToCQRS), @event.ToJson(), "Duplicated message");
                return false;
            }

            _cqrsEngine.SendCommand<ProcessEthCoinEventCommand>(new ProcessEthCoinEventCommand()
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

            return true;
        }

        public async Task<bool> SendHotWalletEventToCQRS(HotWalletEvent @event)
        {
            if (!await _deduplicator.EnsureNotDuplicateAsync(@event))
            {
                await _log.WriteWarningAsync(nameof(EthereumEventsQueue), nameof(SendHotWalletEventToCQRS), @event.ToJson(), "Duplicated message");
                return false;
            }

            _cqrsEngine.SendCommand<ProcessHotWalletErc20EventCommand>(new ProcessHotWalletErc20EventCommand()
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