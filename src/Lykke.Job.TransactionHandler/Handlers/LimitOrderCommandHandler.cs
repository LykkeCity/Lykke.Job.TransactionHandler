using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.LimitTrades;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Service.Assets.Client;
using Lykke.Service.ClientAccount.Client;

namespace Lykke.Job.TransactionHandler.Handlers
{
    [UsedImplicitly]
    public class LimitOrderCommandHandler
    {
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ILimitOrdersRepository _limitOrdersRepository;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        readonly Dictionary<string, bool> _trusted = new Dictionary<string, bool>();
        private readonly ILog _log;

        public LimitOrderCommandHandler(
            IClientAccountClient clientAccountClient,
            ILimitOrdersRepository limitOrdersRepository,
            IAssetsServiceWithCache assetsServiceWithCache,
            ILogFactory logFactory
            )
        {
            _clientAccountClient = clientAccountClient;
            _limitOrdersRepository = limitOrdersRepository;
            _assetsServiceWithCache = assetsServiceWithCache;
            _log = logFactory.CreateLog(this);
        }

        [UsedImplicitly]
        public async Task<CommandHandlingResult> Handle(ProcessLimitOrderCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();
            var stepWatch = new Stopwatch();
            stepWatch.Start();

            try
            {
                var clientId = command.LimitOrder.Order.ClientId;

                 if (!_trusted.ContainsKey(clientId))
                     _trusted[clientId] = (await _clientAccountClient.IsTrustedAsync(clientId)).Value;

                 _log.Info("LimitOrderProcessing", new { TxHandler = new { Step = "01. Client account is trusted", Time = stepWatch.ElapsedMilliseconds}});
                 stepWatch.Restart();

                 var isTrustedClient = _trusted[clientId];

                 var limitOrderExecutedEvent = new LimitOrderExecutedEvent
                 {
                     IsTrustedClient = isTrustedClient,
                     LimitOrder = command.LimitOrder
                 };

                 var tradesWerePerformed = command.LimitOrder.Trades != null && command.LimitOrder.Trades.Any();
                 if (!isTrustedClient)
                 {
                     // need previous order state for not trusted clients
                     var prevOrderState = await _limitOrdersRepository.GetOrderAsync(command.LimitOrder.Order.ClientId, command.LimitOrder.Order.Id);

                     var isImmediateTrade = tradesWerePerformed && command.LimitOrder.Trades.First().Timestamp == command.LimitOrder.Order.Registered;
                     limitOrderExecutedEvent.HasPrevOrderState = prevOrderState != null && !isImmediateTrade;
                     limitOrderExecutedEvent.PrevRemainingVolume = prevOrderState?.RemainingVolume;

                     limitOrderExecutedEvent.Aggregated = AggregateSwaps(limitOrderExecutedEvent.LimitOrder.Trades);
                 }

                 _log.Info("LimitOrderProcessing", new { TxHandler = new { Step = "02. Is not trusted client", Time = stepWatch.ElapsedMilliseconds}});
                 stepWatch.Restart();

                 await _limitOrdersRepository.CreateOrUpdateAsync(command.LimitOrder.Order);

                 _log.Info("LimitOrderProcessing", new { TxHandler = new { Step = "03. Upsert limit order", Time = stepWatch.ElapsedMilliseconds}});
                 stepWatch.Restart();

                 var status = (OrderStatus)Enum.Parse(typeof(OrderStatus), command.LimitOrder.Order.Status);

                 // workaround: ME sends wrong status
                 if (status == OrderStatus.Processing && !tradesWerePerformed)
                     status = OrderStatus.InOrderBook;
                 else if (status == OrderStatus.PartiallyMatched && !tradesWerePerformed)
                     status = OrderStatus.Placed;

                 if (status == OrderStatus.Processing
                     || status == OrderStatus.PartiallyMatched // new version of Processing
                     || status == OrderStatus.Matched
                     || status == OrderStatus.Cancelled)
                 {
                     limitOrderExecutedEvent.Trades = await CreateTrades(command.LimitOrder);
                 }

                 _log.Info("LimitOrderProcessing", new { TxHandler = new { Step = "04. Create trades", Time = stepWatch.ElapsedMilliseconds}});
                 stepWatch.Restart();

                 eventPublisher.PublishEvent(limitOrderExecutedEvent);

                 return CommandHandlingResult.Ok();
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { TxHandler = new { Handler = nameof(LimitOrderCommandHandler),  Command = nameof(ProcessLimitOrderCommand),
                        Time = sw.ElapsedMilliseconds
                    }});
            }
        }

        private async Task<ClientTrade[]> CreateTrades(LimitQueueItem.LimitOrderWithTrades limitOrderWithTrades)
        {
            if (limitOrderWithTrades.Trades == null || limitOrderWithTrades.Trades.Count == 0)
                return new ClientTrade[0];

            var assetPair = await _assetsServiceWithCache.TryGetAssetPairAsync(limitOrderWithTrades.Order.AssetPairId);

            var trades = limitOrderWithTrades.ToDomain(assetPair);

            return trades.ToArray();
        }

        private List<AggregatedTransfer> AggregateSwaps(IEnumerable<LimitQueueItem.LimitTradeInfo> trades)
        {
            var list = new List<AggregatedTransfer>();

            if (trades != null)
            {
                foreach (var swap in trades)
                {
                    var amount1 = Convert.ToDecimal(swap.Volume);
                    var amount2 = Convert.ToDecimal(swap.OppositeVolume);

                    AddAmount(list, swap.ClientId, swap.Asset, -amount1);
                    AddAmount(list, swap.OppositeClientId, swap.Asset, amount1);

                    AddAmount(list, swap.OppositeClientId, swap.OppositeAsset, -amount2);
                    AddAmount(list, swap.ClientId, swap.OppositeAsset, amount2);
                }
            }

            return list;
        }

        private void AddAmount(ICollection<AggregatedTransfer> list, string client, string asset, decimal amount)
        {
            var client1 = list.FirstOrDefault(x => x.ClientId == client && x.AssetId == asset);
            if (client1 != null)
                client1.Amount += amount;
            else
                list.Add(new AggregatedTransfer
                {
                    Amount = amount,
                    ClientId = client,
                    AssetId = asset,
                    TransferId = Guid.NewGuid().ToString()
                });
        }
    }

    public class AggregatedTransfer
    {
        public string ClientId { get; set; }

        public string AssetId { get; set; }

        public decimal Amount { get; set; }

        public string TransferId { get; set; }
    }

    public class ClientTrade
    {
        public string Id { get; set; }
        public string ClientId { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }
        public DateTime DateTime { get; set; }
        public double Price { get; set; }
        public string LimitOrderId { get; set; }
        public string OppositeLimitOrderId { get; set; }
        public string TransactionId { get; set; }
        public bool IsLimitOrderResult { get; set; }
        public Service.OperationsRepository.AutorestClient.Models.TransactionStates State { get; set; }
        public double FeeSize { get; set; }
        public Service.OperationsRepository.AutorestClient.Models.FeeType FeeType { get; set; }
    }
}
