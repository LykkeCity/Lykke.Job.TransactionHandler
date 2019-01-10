using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Sagas;
using Lykke.Job.TransactionHandler.Sagas.Services;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class TradeCommandHandler
    {
        private readonly ITransactionsRepository _transactionsRepository;
        private readonly ITransactionService _transactionService;
        private readonly IContextFactory _contextFactory;

        private readonly ILog _log;

        public TradeCommandHandler(
            [NotNull] ILog log,
            [NotNull] ITransactionsRepository transactionsRepository,
            [NotNull] ITransactionService transactionService,
            [NotNull] IContextFactory contextFactory)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _transactionsRepository = transactionsRepository ?? throw new ArgumentNullException(nameof(transactionsRepository));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<CommandHandlingResult> Handle(CreateTradeCommand command, IEventPublisher eventPublisher)
        {
            var queueMessage = command.QueueMessage;

            var clientId = queueMessage.Order.ClientId;

            if (!queueMessage.Order.Status.Equals("matched", StringComparison.OrdinalIgnoreCase))
            {
                _log.WriteInfo($"{nameof(TradeSaga)}:{nameof(TradeCommandHandler)}", queueMessage.ToJson(),
                    "Message processing being aborted, due to order status is not matched");

                return CommandHandlingResult.Ok();
            }

            var context = await _transactionService.GetTransactionContext<SwapOffchainContextData>(queueMessage.Order.Id) ?? new SwapOffchainContextData();

            await _contextFactory.FillTradeContext(context, queueMessage.Order, queueMessage.Trades, clientId);

            ChaosKitty.Meow();

            await _transactionService.SetTransactionContext(queueMessage.Order.Id, context);

            ChaosKitty.Meow();

            eventPublisher.PublishEvent(new TradeCreatedEvent
            {
                OrderId = queueMessage.Order.Id,
                IsTrustedClient = context.IsTrustedClient,
                MarketOrder = context.Order,
                ClientTrades = context.ClientTrades,
                QueueMessage = queueMessage
            });

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(CreateTransactionCommand command)
        {
            await _transactionsRepository.TryCreateAsync(command.OrderId, BitCoinCommands.SwapOffchain, "", null, "");

            ChaosKitty.Meow();

            return CommandHandlingResult.Ok();
        }
    }
}