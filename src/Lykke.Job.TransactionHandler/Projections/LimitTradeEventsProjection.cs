using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Commands.LimitTrades;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class LimitTradeEventsProjection
    {
        private readonly ILog _log;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly ILimitTradeEventsRepositoryClient _limitTradeEventsRepositoryClient;

        public LimitTradeEventsProjection(ILog log, IAssetsServiceWithCache assetsServiceWithCache, ILimitTradeEventsRepositoryClient limitTradeEventsRepositoryClient)
        {
            _log = log;
            _assetsServiceWithCache = assetsServiceWithCache;
            _limitTradeEventsRepositoryClient = limitTradeEventsRepositoryClient;
        }

        public async Task Handle(LimitOrderExecutedEvent evt)
        {
            if (evt.IsTrustedClient)
                return;
            
            var status = (OrderStatus)Enum.Parse(typeof(OrderStatus), evt.LimitOrder.Order.Status);

            switch (status)
            {
                case OrderStatus.InOrderBook:
                case OrderStatus.Cancelled:
                    await CreateEvent(evt.LimitOrder, status);
                    break;
                case OrderStatus.Processing:
                case OrderStatus.Matched:
                    if (!evt.HasPrevOrderState)
                        await CreateEvent(evt.LimitOrder, OrderStatus.InOrderBook);
                    break;
                case OrderStatus.Dust:
                case OrderStatus.NoLiquidity:
                case OrderStatus.NotEnoughFunds:
                case OrderStatus.ReservedVolumeGreaterThanBalance:
                case OrderStatus.UnknownAsset:
                case OrderStatus.LeadToNegativeSpread:
                    _log.WriteInfo(nameof(ProcessLimitOrderCommand), evt.LimitOrder.ToJson(), $"Client {evt.LimitOrder.Order.ClientId}. Order {evt.LimitOrder.Order.Id}: Rejected");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(OrderStatus));
            }

            ChaosKitty.Meow();
        }

        private async Task CreateEvent(LimitQueueItem.LimitOrderWithTrades limitOrderWithTrades, OrderStatus status)
        {
            var order = limitOrderWithTrades.Order;
            var type = order.Volume > 0 ? OrderType.Buy : OrderType.Sell;
            var assetPair = await _assetsServiceWithCache.TryGetAssetPairAsync(order.AssetPairId);
            var date = status == OrderStatus.InOrderBook ? limitOrderWithTrades.Order.CreatedAt : DateTime.UtcNow;

            var insertRequest = new LimitTradeEventInsertRequest
            {
                Volume = order.Volume,
                Type = type,
                OrderId = order.Id,
                Status = status,
                AssetId = assetPair?.BaseAssetId,
                ClientId = order.ClientId,
                Price = order.Price,
                AssetPair = order.AssetPairId,
                DateTime = date
            };

            await _limitTradeEventsRepositoryClient.CreateAsync(insertRequest);

            _log.WriteInfo(nameof(LimitTradeEventsProjection), JsonConvert.SerializeObject(insertRequest, Formatting.Indented), $"Client {order.ClientId}. Limit trade {order.Id}. State has changed -> {status}");
        }
    }
}