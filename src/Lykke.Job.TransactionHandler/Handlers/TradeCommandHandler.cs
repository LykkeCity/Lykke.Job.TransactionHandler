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
            await _log.WriteInfoAsync(nameof(TradeCommandHandler), nameof(CreateTradeCommand), command.ToJson());

            ChaosKitty.Meow();

            var queueMessage = command.QueueMessage;

            var clientId = queueMessage.Order.ClientId;

            if (!queueMessage.Order.Status.Equals("matched", StringComparison.OrdinalIgnoreCase))
            {
                await _log.WriteInfoAsync(nameof(TradeSaga), nameof(TradeCommandHandler), queueMessage.ToJson(), "Message processing being aborted, due to order status is not matched");

                return CommandHandlingResult.Ok(); // todo: Fail?
            }

            var context = await _transactionService.GetTransactionContext<SwapOffchainContextData>(queueMessage.Order.Id) ?? new SwapOffchainContextData();

            await _contextFactory.FillTradeContext(context, queueMessage.Order, queueMessage.Trades, clientId);
            await _transactionService.SetTransactionContext(queueMessage.Order.Id, context);

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

        public async Task<CommandHandlingResult> Handle(CreateTransactionCommand command, IEventPublisher eventPublisher)
        {
            await _log.WriteInfoAsync(nameof(TradeCommandHandler), nameof(CreateTransactionCommand), command.ToJson());

            ChaosKitty.Meow();

            var transaction = _transactionsRepository.FindByTransactionIdAsync(command.OrderId);

            if (transaction == null)
            {
                await _transactionsRepository.CreateAsync(command.OrderId, BitCoinCommands.SwapOffchain, "", null, "");
            }

            eventPublisher.PublishEvent(new TransactionCreatedEvent { OrderId = command.OrderId });

            return CommandHandlingResult.Ok();
        }

        //public async Task Handle(CreateLimitTradeCommand command, IEventPublisher eventPublisher)
        //{
        //    var limitOrderWithTrades = command.LimitOrder;

        //    var meOrder = limitOrderWithTrades.Order;

        //    var isTrusted = (await _clientAccountClient.IsTrustedAsync(meOrder.ClientId)).Value;

        //    var aggregated = AggregateSwaps(limitOrderWithTrades.Trades);

        //    ILimitOrder prevOrderState = null;

        //    // need previous order state for not trusted clients
        //    if (!isTrusted)
        //        prevOrderState = await _limitOrdersRepository.GetOrderAsync(meOrder.Id);

        //    await _limitOrdersRepository.CreateOrUpdateAsync(meOrder);

        //    var status = (OrderStatus)Enum.Parse(typeof(OrderStatus), meOrder.Status);

        //    IClientTrade[] trades = null;
        //    if (status == OrderStatus.Processing || status == OrderStatus.Matched)
        //        trades = await SaveTrades(limitOrderWithTrades);

        //    // all code below is for untrusted users
        //    if (isTrusted)
        //        return;

        //    await SaveTransactionAndContext(trades, aggregated, limitOrderWithTrades);

        //    switch (status)
        //    {
        //        case OrderStatus.InOrderBook:
        //        case OrderStatus.Cancelled:
        //            await CreateEvent(limitOrderWithTrades, status);
        //            break;
        //        case OrderStatus.Processing:
        //        case OrderStatus.Matched:
        //            if (prevOrderState == null)
        //                await CreateEvent(limitOrderWithTrades, OrderStatus.InOrderBook);
        //            await SendMoney(trades, aggregated, meOrder, status);
        //            break;
        //        case OrderStatus.Dust:
        //        case OrderStatus.NoLiquidity:
        //        case OrderStatus.NotEnoughFunds:
        //        case OrderStatus.ReservedVolumeGreaterThanBalance:
        //        case OrderStatus.UnknownAsset:
        //        case OrderStatus.LeadToNegativeSpread:
        //            await _log.WriteInfoAsync(nameof(LimitTradeQueue), nameof(ProcessMessage), limitOrderWithTrades.ToJson(), "order rejected");
        //            break;
        //        default:
        //            throw new ArgumentOutOfRangeException(nameof(OrderStatus));
        //    }

        //    if (status == OrderStatus.Cancelled || status == OrderStatus.Matched)
        //        await ReturnRemainingVolume(limitOrderWithTrades);

        //    await UpdateCache(meOrder);

        //    await SendPush(aggregated, meOrder, prevOrderState, status);
        //}

        //private async Task<IClientTrade[]> SaveTrades(LimitQueueItem.LimitOrderWithTrades limitOrderWithTrades)
        //{
        //    if (limitOrderWithTrades.Trades.Count == 0)
        //        return new IClientTrade[0];

        //    var walletCredsClientA = await _walletCredentialsRepository.GetAsync(limitOrderWithTrades.Trades[0].ClientId);
        //    var walletCredsClientB = await _walletCredentialsRepository.GetAsync(limitOrderWithTrades.Trades[0].OppositeClientId);

        //    var trades = limitOrderWithTrades.ToDomainOffchain(limitOrderWithTrades.Order.Id, walletCredsClientA, walletCredsClientB);

        //    foreach (var trade in trades)
        //    {
        //        var tradeAsset = await _assetsServiceWithCache.TryGetAssetAsync(trade.AssetId);

        //        // already settled guarantee transaction or trusted asset
        //        if (trade.Amount < 0 || tradeAsset.IsTrusted)
        //            trade.State = TransactionStates.SettledOffchain;
        //        else
        //            trade.State = TransactionStates.InProcessOffchain;
        //    }

        //    await _clientTradesRepository.SaveAsync(trades);

        //    return trades;
        //}

        //private async Task<IClientTrade[]> SaveTransactionAndContext(IClientTrade[] trades, List<AggregatedTransfer> operations, LimitQueueItem.LimitOrderWithTrades limitOrderWithTrades)
        //{
        //    var contextData = await _transactionService.GetTransactionContext<SwapOffchainContextData>(limitOrderWithTrades.Order.Id) ?? new SwapOffchainContextData();

        //    foreach (var operation in operations.Where(x => x.ClientId == limitOrderWithTrades.Order.ClientId))
        //    {
        //        var trade = trades.FirstOrDefault(x => x.ClientId == operation.ClientId && x.AssetId == operation.AssetId && Math.Abs(x.Amount - (double)operation.Amount) < 0.00000001);

        //        contextData.Operations.Add(new SwapOffchainContextData.Operation()
        //        {
        //            TransactionId = operation.TransferId,
        //            Amount = operation.Amount,
        //            ClientId = operation.ClientId,
        //            AssetId = operation.AssetId,
        //            ClientTradeId = trade?.Id
        //        });
        //    }

        //    await _transactionService.CreateOrUpdateAsync(limitOrderWithTrades.Order.Id);
        //    await _transactionService.SetTransactionContext(limitOrderWithTrades.Order.Id, contextData);

        //    return trades;
        //}

        //private async Task SendMoney(IClientTrade[] clientTrades, IEnumerable<AggregatedTransfer> aggregatedTransfers, ILimitOrder order, OrderStatus status)
        //{
        //    var clientId = order.ClientId;

        //    var executed = aggregatedTransfers.FirstOrDefault(x => x.ClientId == clientId && x.Amount > 0);

        //    var asset = await _assetsServiceWithCache.TryGetAssetAsync(executed.AssetId);

        //    if (asset.IsTrusted)
        //        return;

        //    if (asset.Blockchain == Blockchain.Ethereum)
        //    {
        //        await ProcessEthBuy(executed, asset, clientTrades, order.Id);
        //        return;
        //    }

        //    if (status == OrderStatus.Matched)
        //        await _offchainRequestService.CreateOffchainRequestAndUnlock(executed.TransferId, clientId, executed.AssetId, executed.Amount, order.Id, OffchainTransferType.FromHub);
        //    else
        //        await _offchainRequestService.CreateOffchainRequestAndLock(executed.TransferId, clientId, executed.AssetId, executed.Amount, order.Id, OffchainTransferType.FromHub);
        //}

        //private async Task SendPush(IEnumerable<AggregatedTransfer> aggregatedTransfers, ILimitOrder order, ILimitOrder prevOrderState, OrderStatus status)
        //{
        //    var clientId = order.ClientId;
        //    var type = order.Volume > 0 ? OrderType.Buy : OrderType.Sell;
        //    var typeString = type.ToString().ToLower();
        //    var assetPair = await _assetsServiceWithCache.TryGetAssetPairAsync(order.AssetPairId);

        //    var receivedAsset = type == OrderType.Buy ? assetPair.BaseAssetId : assetPair.QuotingAssetId;
        //    var receivedAssetEntity = await _assetsServiceWithCache.TryGetAssetAsync(receivedAsset);

        //    var priceAsset = await _assetsServiceWithCache.TryGetAssetAsync(assetPair.QuotingAssetId);

        //    var volume = (decimal)Math.Abs(order.Volume);
        //    var remainingVolume = (decimal)Math.Abs(prevOrderState?.RemainingVolume ?? order.Volume);
        //    var executedSum = Math.Abs(aggregatedTransfers.Where(x => x.ClientId == clientId && x.AssetId == receivedAsset)
        //                        .Select(x => x.Amount)
        //                        .DefaultIfEmpty(0)
        //                        .Sum()).TruncateDecimalPlaces(receivedAssetEntity.Accuracy);

        //    string msg;

        //    switch (status)
        //    {
        //        case OrderStatus.InOrderBook:
        //            msg = string.Format(TextResources.LimitOrderStarted, typeString, order.AssetPairId, volume, order.Price, priceAsset.DisplayId);
        //            break;
        //        case OrderStatus.Cancelled:
        //            msg = string.Format(TextResources.LimitOrderCancelled, typeString, order.AssetPairId, volume, order.Price, priceAsset.DisplayId);
        //            break;
        //        case OrderStatus.Processing:
        //            msg = string.Format(TextResources.LimitOrderPartiallyExecuted, typeString, order.AssetPairId, remainingVolume, order.Price, priceAsset.DisplayId, executedSum, receivedAssetEntity.DisplayId);
        //            break;
        //        case OrderStatus.Matched:
        //            msg = string.Format(TextResources.LimitOrderExecuted, typeString, order.AssetPairId, remainingVolume, order.Price, priceAsset.DisplayId, executedSum, receivedAssetEntity.DisplayId);
        //            break;
        //        case OrderStatus.Dust:
        //        case OrderStatus.NoLiquidity:
        //        case OrderStatus.NotEnoughFunds:
        //        case OrderStatus.ReservedVolumeGreaterThanBalance:
        //        case OrderStatus.UnknownAsset:
        //        case OrderStatus.LeadToNegativeSpread:
        //            msg = string.Format(TextResources.LimitOrderRejected, typeString, order.AssetPairId, volume, order.Price, priceAsset.DisplayId);
        //            break;
        //        default:
        //            throw new ArgumentOutOfRangeException(nameof(OrderStatus));
        //    }

        //    var pushSettings = await _clientSettingsRepository.GetSettings<PushNotificationsSettings>(clientId);
        //    if (pushSettings.Enabled)
        //    {
        //        var clientAcc = await _clientAccountClient.GetByIdAsync(clientId);

        //        await _appNotifications.SendLimitOrderNotification(new[] { clientAcc.NotificationsId }, msg, type, status);
        //    }
        //}

        //private async Task ReturnRemainingVolume(LimitQueueItem.LimitOrderWithTrades limitOrderWithTrades)
        //{
        //    var order = limitOrderWithTrades.Order;
        //    var offchainOrder = await _ordersRepository.GetOrder(order.Id);

        //    var type = order.Volume > 0 ? OrderType.Buy : OrderType.Sell;
        //    var assetPair = await _assetsServiceWithCache.TryGetAssetPairAsync(order.AssetPairId);
        //    var neededAsset = type == OrderType.Buy ? assetPair.QuotingAssetId : assetPair.BaseAssetId;
        //    var asset = await _assetsServiceWithCache.TryGetAssetAsync(neededAsset);

        //    if (asset.IsTrusted)
        //        return;

        //    if (type == OrderType.Buy)
        //    {
        //        var initial = offchainOrder.ReservedVolume;

        //        var trades = await _clientTradesRepository.GetByOrderAsync(order.Id);

        //        var executed = trades.Where(x => x.AssetId == neededAsset && x.ClientId == order.ClientId)
        //            .Select(x => x.Amount).DefaultIfEmpty(0).Sum();

        //        var returnAmount = Math.Max(0, initial - Math.Abs((decimal)executed));

        //        if (asset.Blockchain == Blockchain.Ethereum)
        //        {
        //            // if order partially or fully executed then broadcast guarantee transfer
        //            if (offchainOrder.Volume > returnAmount)
        //                await ProcessEthGuaranteeTransfer(order.Id, returnAmount);
        //            return;
        //        }

        //        if (returnAmount > 0)
        //        {
        //            await _offchainRequestService.CreateOffchainRequestAndUnlock(Guid.NewGuid().ToString(),
        //                order.ClientId,
        //                neededAsset, returnAmount, order.Id, OffchainTransferType.FromHub);
        //        }
        //    }
        //    else
        //    {
        //        var remainigVolume = Math.Abs((decimal)order.RemainingVolume);

        //        if (asset.Blockchain == Blockchain.Ethereum)
        //        {
        //            var initialVolume = Math.Abs(offchainOrder.Volume);
        //            // if order partially or fully executed then broadcast guarantee transfer
        //            // if initialVolume == remainigVolume then user cancelled limit order without partial executing
        //            if (initialVolume > remainigVolume)
        //                await ProcessEthGuaranteeTransfer(order.Id, remainigVolume);
        //            return;
        //        }

        //        if (remainigVolume > 0)
        //        {
        //            await _offchainRequestService.CreateOffchainRequestAndUnlock(Guid.NewGuid().ToString(), order.ClientId,
        //                neededAsset, remainigVolume, order.Id, OffchainTransferType.FromHub);
        //        }
        //    }
        //}

        //private async Task CreateEvent(LimitQueueItem.LimitOrderWithTrades limitOrderWithTrades, OrderStatus status)
        //{
        //    var order = limitOrderWithTrades.Order;
        //    var type = order.Volume > 0 ? OrderType.Buy : OrderType.Sell;
        //    var assetPair = await _assetsServiceWithCache.TryGetAssetPairAsync(order.AssetPairId);
        //    var date = status == OrderStatus.InOrderBook ? limitOrderWithTrades.Order.CreatedAt : DateTime.UtcNow;
        //    await _limitTradeEventsRepository.CreateEvent(order.Id, order.ClientId, type, order.Volume,
        //        assetPair?.BaseAssetId, order.AssetPairId, order.Price, status, date);
        //}

        //private async Task UpdateCache(IOrderBase meOrder)
        //{
        //    var count = (await _limitOrdersRepository.GetActiveOrdersAsync(meOrder.ClientId)).Count();

        //    await _clientCacheRepository.UpdateLimitOrdersCount(meOrder.ClientId, count);
        //}

        //private async Task ProcessEthBuy(AggregatedTransfer operation, Asset asset, IClientTrade[] clientTrades, string orderId)
        //{
        //    string errMsg = string.Empty;
        //    var transferId = Guid.NewGuid();

        //    try
        //    {
        //        var toAddress = await _bcnClientCredentialsRepository.GetClientAddress(operation.ClientId);

        //        await _ethereumTransactionRequestRepository.InsertAsync(new EthereumTransactionRequest
        //        {
        //            AddressTo = toAddress,
        //            AssetId = asset.Id,
        //            ClientId = operation.ClientId,
        //            Id = transferId,
        //            OperationIds =
        //                clientTrades.Where(x => x.ClientId == operation.ClientId && x.Amount > 0)
        //                    .Select(x => x.Id)
        //                    .ToArray(),
        //            OperationType = OperationType.Trade,
        //            OrderId = orderId,
        //            Volume = operation.Amount
        //        }, false);

        //        var res = await _srvEthereumHelper.SendTransferAsync(transferId, string.Empty, asset,
        //            _settings.HotwalletAddress, toAddress, operation.Amount);

        //        if (res.HasError)
        //        {
        //            errMsg = res.Error.ToJson();

        //            await _log.WriteWarningAsync(nameof(TradeQueue), nameof(ProcessEthGuaranteeTransfer), errMsg, string.Empty);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        await _log.WriteErrorAsync(nameof(TradeQueue), nameof(ProcessEthGuaranteeTransfer), e.Message, e);

        //        errMsg = $"{e.GetType()}\n{e.Message}";
        //    }

        //    if (!string.IsNullOrEmpty(errMsg))
        //    {
        //        await _ethClientEventLogs.WriteEvent(operation.ClientId, Event.Error, new
        //        {
        //            Info = $"{asset.Id} was not transferred to client",
        //            RequestId = transferId,
        //            Operation = operation.ToJson(),
        //            Error = errMsg
        //        }.ToJson());
        //    }
        //}

        //private async Task<bool> ProcessEthGuaranteeTransfer(string orderId, decimal change)
        //{
        //    var ethereumTxRequest = await _ethereumTransactionRequestRepository.GetByOrderAsync(orderId);

        //    var errMsg = string.Empty;
        //    var asset = await _assetsServiceWithCache.TryGetAssetAsync(ethereumTxRequest.AssetId);
        //    try
        //    {
        //        var fromAddress = await _bcnClientCredentialsRepository.GetAsync(ethereumTxRequest.ClientId, asset.Id);

        //        EthereumResponse<OperationResponse> res;
        //        var minAmountForAsset = (decimal)Math.Pow(10, -asset.Accuracy);
        //        if (change > 0 && Math.Abs(change) >= minAmountForAsset)
        //        {
        //            res = await _srvEthereumHelper.SendTransferWithChangeAsync(change,
        //                ethereumTxRequest.SignedTransfer.Sign, ethereumTxRequest.SignedTransfer.Id,
        //                asset, fromAddress.Address, _settings.HotwalletAddress, ethereumTxRequest.Volume);
        //        }
        //        else
        //        {
        //            res = await _srvEthereumHelper.SendTransferAsync(ethereumTxRequest.SignedTransfer.Id, ethereumTxRequest.SignedTransfer.Sign,
        //                asset, fromAddress.Address, _settings.HotwalletAddress, ethereumTxRequest.Volume);
        //        }

        //        if (res.HasError)
        //        {
        //            errMsg = res.Error.ToJson();
        //            await _log.WriteWarningAsync(nameof(TradeQueue), nameof(ProcessEthGuaranteeTransfer), errMsg, string.Empty);
        //        }


        //        var trades = await _clientTradesRepository.GetByOrderAsync(orderId);
        //        ethereumTxRequest.OperationIds =
        //            trades.Where(x => x.ClientId == ethereumTxRequest.ClientId && x.Amount < 0 && x.AssetId == asset.Id)
        //                .Select(x => x.Id)
        //                .ToArray();
        //        await _ethereumTransactionRequestRepository.UpdateAsync(ethereumTxRequest);
        //    }
        //    catch (Exception e)
        //    {
        //        await _log.WriteErrorAsync(nameof(TradeQueue), nameof(ProcessEthGuaranteeTransfer), e.Message, e);

        //        errMsg = $"{e.GetType()}\n{e.Message}";
        //    }

        //    if (!string.IsNullOrEmpty(errMsg))
        //    {
        //        await _ethClientEventLogs.WriteEvent(ethereumTxRequest.ClientId, Event.Error, new
        //        {
        //            Info = $"Guarantee transfer of {asset.Id} failed",
        //            RequestId = ethereumTxRequest.Id,
        //            Error = errMsg
        //        }.ToJson());
        //        return false;
        //    }

        //    return true;
        //}

        //private List<AggregatedTransfer> AggregateSwaps(IEnumerable<LimitQueueItem.LimitTradeInfo> trades)
        //{
        //    var list = new List<AggregatedTransfer>();

        //    foreach (var swap in trades)
        //    {
        //        var amount1 = Convert.ToDecimal(swap.Volume);
        //        var amount2 = Convert.ToDecimal(swap.OppositeVolume);

        //        AddAmount(list, swap.ClientId, swap.Asset, -amount1);
        //        AddAmount(list, swap.OppositeClientId, swap.Asset, amount1);

        //        AddAmount(list, swap.OppositeClientId, swap.OppositeAsset, -amount2);
        //        AddAmount(list, swap.ClientId, swap.OppositeAsset, amount2);
        //    }

        //    return list;
        //}

        //private void AddAmount(ICollection<AggregatedTransfer> list, string client, string asset, decimal amount)
        //{
        //    var client1 = list.FirstOrDefault(x => x.ClientId == client && x.AssetId == asset);
        //    if (client1 != null)
        //        client1.Amount += amount;
        //    else
        //        list.Add(new AggregatedTransfer
        //        {
        //            Amount = amount,
        //            ClientId = client,
        //            AssetId = asset,
        //            TransferId = Guid.NewGuid().ToString()
        //        });
        //}

        private class AggregatedTransfer
        {
            public string ClientId { get; set; }

            public string AssetId { get; set; }

            public decimal Amount { get; set; }

            public string TransferId { get; set; }
        }
    }
}