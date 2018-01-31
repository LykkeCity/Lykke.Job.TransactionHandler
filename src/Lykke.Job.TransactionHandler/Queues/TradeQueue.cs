using System;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Cqrs;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Fee;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class TradeQueue : IQueueSubscriber
    {
#if DEBUG
        private const string QueueName = "transactions.trades-dev";
        private const bool QueueDurable = false;
#else
        private const string QueueName = "transactions.trades";
        private const bool QueueDurable = true;
#endif

        private readonly ILog _log;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly IFeeLogService _feeLogService;

        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<TradeQueueItem> _subscriber;

        public TradeQueue(
            AppSettings.RabbitMqSettings config,
            ILog log,
            ICqrsEngine cqrsEngine,
			IFeeLogService feeLogService)
        {
            _rabbitConfig = config;
            _log = log;
            _cqrsEngine = cqrsEngine;
            _feeLogService = feeLogService ?? throw new ArgumentException(nameof(feeLogService));
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.ConnectionString,
                QueueName = QueueName,
                ExchangeName = _rabbitConfig.ExchangeSwap,
                DeadLetterExchangeName = $"{_rabbitConfig.ExchangeSwap}.dlx",
                RoutingKey = "",
                IsDurable = QueueDurable
            };

            try
            {
                _subscriber = new RabbitMqSubscriber<TradeQueueItem>(
                        settings,
                        new ResilientErrorHandlingStrategy(_log, settings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_log, settings)))
                    .SetMessageDeserializer(new JsonMessageDeserializer<TradeQueueItem>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .Subscribe(ProcessMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(TradeQueue), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        private async Task ProcessMessage(TradeQueueItem queueMessage)
        {
            await _feeLogService.WriteFeeInfo(queueMessage);
            
            _cqrsEngine.SendCommand(new Commands.CreateTradeCommand
            {
                QueueMessage = queueMessage
            }, BoundedContexts.TxHandler, BoundedContexts.Trades);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}