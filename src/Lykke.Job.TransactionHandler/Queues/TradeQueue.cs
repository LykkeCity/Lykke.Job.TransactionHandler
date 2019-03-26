﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.LimitTrades;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.MatchingEngine.Connector.Models.Events;
using Lykke.MatchingEngine.Connector.Models.Events.Common;
using Lykke.RabbitMq.Mongo.Deduplicator;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Service.Assets.Client;

namespace Lykke.Job.TransactionHandler.Queues
{
    public sealed class TradeQueue : IQueueSubscriber
    {
        private const bool QueueDurable = true;

        private readonly ILog _log;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly AppSettings.TransactionHandlerSettings _settings;
        private readonly AppSettings.RabbitMqSettings _rabbitConfig;

        private RabbitMqSubscriber<ExecutionEvent> _subscriber;

        public TradeQueue(
            AppSettings.RabbitMqSettings config,
            ILog log,
            ICqrsEngine cqrsEngine,
            IAssetsServiceWithCache assetsServiceWithCache,
            AppSettings.TransactionHandlerSettings settings
            )
        {
            _rabbitConfig = config;
            _log = log;
            _cqrsEngine = cqrsEngine;
            _assetsServiceWithCache = assetsServiceWithCache;
            _settings = settings;
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.NewMeRabbitConnString,
                QueueName = $"{_rabbitConfig.EventsExchange}.orders.txhandler",
                ExchangeName = _rabbitConfig.EventsExchange,
                RoutingKey = ((int)MessageType.Order).ToString(),
                IsDurable = QueueDurable
            };
            settings.DeadLetterExchangeName = $"{settings.QueueName}.dlx";

            try
            {
                _subscriber = new RabbitMqSubscriber<ExecutionEvent>(
                        settings,
                        new ResilientErrorHandlingStrategy(_log, settings,
                            retryTimeout: TimeSpan.FromSeconds(20),
                            retryNum: 3,
                            next: new DeadQueueErrorHandlingStrategy(_log, settings)))
                    .SetMessageDeserializer(new ProtobufMessageDeserializer<ExecutionEvent>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .SetAlternativeExchange(_rabbitConfig.AlternateConnectionString)
                    .SetDeduplicator(MongoStorageDeduplicator.Create(_settings.MongoDeduplicator.ConnectionString, _settings.MongoDeduplicator.CollectionName))
                    .Subscribe(ProcessMessage)
                    .CreateDefaultBinding()
                    .SetPrefetchCount(300)
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteError(nameof(TradeQueue), nameof(Start), ex);
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

        private Task ProcessMessage(ExecutionEvent evt)
        {
            _log.WriteInfo(nameof(TradeQueue), nameof(ProcessMessage), evt.ToJson());

            foreach (var order in evt.Orders)
            {
                switch (order.OrderType)
                {
                    case OrderType.Market:
                        var marketOrder = ToOldMarketOrder(order);
                        _cqrsEngine.SendCommand(
                            new Commands.CreateTradeCommand { QueueMessage = marketOrder },
                            BoundedContexts.TxHandler,
                            BoundedContexts.Trades);
                        break;
                    case OrderType.Limit:
                    case OrderType.StopLimit:
                        var limitOrder = ToOldLimitOrder(order);
                        _cqrsEngine.SendCommand(
                            new ProcessLimitOrderCommand { LimitOrder = limitOrder },
                            BoundedContexts.TxHandler,
                            BoundedContexts.TxHandler);
                        break;
                    default:
                        throw new NotSupportedException($"Order type {order.OrderType} is not supported");
                }
            }

            return Task.CompletedTask;
        }

        private TradeQueueItem ToOldMarketOrder(Order order)
        {
            return new TradeQueueItem
            {
                Order = new TradeQueueItem.MarketOrder
                {
                    Id = order.ExternalId,
                    MatchingId = order.Id,
                    AssetPairId = order.AssetPairId,
                    ClientId = order.WalletId,
                    Status = order.Status.ToString(),
                    Straight = order.Straight,
                    CreatedAt = order.CreatedAt,
                    Registered = order.Registered,
                    MatchedAt = order.LastMatchTime,
                    Volume = decimal.TryParse(order.Volume, out var volume) ? (double)volume : double.Parse(order.Volume),
                    Price = decimal.TryParse(order.Price, out var price) ? (double)price : (double?)null,
                    ReservedLimitVolume = decimal.TryParse(order.RemainingVolume, out var remaining) ? (double)remaining : 0,
                },
                Trades = ToOldTradeInfos(order.Trades, order),
            };
        }

        private List<TradeQueueItem.TradeInfo> ToOldTradeInfos(List<Trade> trades, Order order)
        {
            if (trades == null)
                return null;

            var result = new List<TradeQueueItem.TradeInfo>(trades.Count);
            foreach (var trade in trades)
            {
                var item = new TradeQueueItem.TradeInfo
                {
                    Price = decimal.TryParse(trade.Price, out var price) ? (double) price : (double?) null,
                    LimitOrderId = trade.OppositeOrderId,
                    LimitOrderExternalId = trade.OppositeExternalOrderId,
                    Timestamp = trade.Timestamp,
                    MarketClientId = order.WalletId,
                    LimitClientId = trade.OppositeWalletId,
                    Fees = trade.Fees?.ToOldFees(order.Fees, trade.Timestamp),
                };
                if (IsBaseAssetMain(
                    order.Straight,
                    order.AssetPairId,
                    trade.BaseAssetId,
                    trade.QuotingAssetId))
                {
                    item.MarketAsset = trade.BaseAssetId;
                    item.MarketVolume = decimal.TryParse(trade.BaseVolume, out var baseVolume)
                        ? (double) baseVolume
                        : double.Parse(trade.BaseVolume);
                    item.LimitAsset = trade.QuotingAssetId;
                    item.LimitVolume = decimal.TryParse(trade.QuotingVolume, out var quoteVolume)
                        ? (double)quoteVolume
                        : double.Parse(trade.QuotingVolume);
                }
                else
                {
                    item.LimitAsset = trade.BaseAssetId;
                    item.LimitVolume = decimal.TryParse(trade.BaseVolume, out var baseVolume)
                        ? (double)baseVolume
                        : double.Parse(trade.BaseVolume);
                    item.MarketAsset = trade.QuotingAssetId;
                    item.MarketVolume = decimal.TryParse(trade.QuotingVolume, out var quoteVolume)
                        ? (double)quoteVolume
                        : double.Parse(trade.QuotingVolume);
                }
                result.Add(item);
            }
            return result;
        }

        private LimitQueueItem.LimitOrderWithTrades ToOldLimitOrder(Order order)
        {
            return new LimitQueueItem.LimitOrderWithTrades
            {
                Order = new LimitQueueItem.LimitOrder
                {
                    Id = order.ExternalId,
                    MatchingId = order.Id,
                    AssetPairId = order.AssetPairId,
                    ClientId = order.WalletId,
                    Status = order.Status.ToString(),
                    Straight = order.Straight,
                    CreatedAt = order.CreatedAt,
                    Registered = order.Registered,
                    Volume = decimal.TryParse(order.Volume, out var volume) ? (double)volume : double.Parse(order.Volume),
                    Price = decimal.TryParse(order.Price, out var price) ? (double)price : double.Parse(order.Price),
                    RemainingVolume = decimal.TryParse(order.RemainingVolume, out var remaining) ? (double)remaining : double.Parse(order.RemainingVolume),
                },
                Trades = ToOldLimitTradeInfos(order.Trades, order),
            };
        }

        private List<LimitQueueItem.LimitTradeInfo> ToOldLimitTradeInfos(List<Trade> trades, Order order)
        {
            if (trades == null)
                return null;

            var result = new List<LimitQueueItem.LimitTradeInfo>(trades.Count);
            foreach (var trade in trades)
            {
                var item = new LimitQueueItem.LimitTradeInfo
                {
                    TradeId = trade.TradeId,
                    Price = decimal.TryParse(trade.Price, out var price) ? (double)price : double.Parse(trade.Price),
                    OppositeOrderId = trade.OppositeOrderId,
                    OppositeOrderExternalId = trade.OppositeExternalOrderId,
                    Timestamp = trade.Timestamp,
                    ClientId = order.WalletId,
                    OppositeClientId = trade.OppositeWalletId,
                    Fees = trade.Fees?.ToOldFees(order.Fees, trade.Timestamp),
                };
                if (IsBaseAssetMain(
                    order.Straight,
                    order.AssetPairId,
                    trade.BaseAssetId,
                    trade.QuotingAssetId))
                {
                    item.Asset = trade.BaseAssetId;
                    item.Volume = decimal.TryParse(trade.BaseVolume, out var baseVolume)
                        ? (double)baseVolume
                        : double.Parse(trade.BaseVolume);
                    item.OppositeAsset = trade.QuotingAssetId;
                    item.OppositeVolume = decimal.TryParse(trade.QuotingVolume, out var quoteVolume)
                        ? (double)quoteVolume
                        : double.Parse(trade.QuotingVolume);
                }
                else
                {
                    item.OppositeAsset = trade.BaseAssetId;
                    item.OppositeVolume = decimal.TryParse(trade.BaseVolume, out var baseVolume)
                        ? (double)baseVolume
                        : double.Parse(trade.BaseVolume);
                    item.Asset = trade.QuotingAssetId;
                    item.Volume = decimal.TryParse(trade.QuotingVolume, out var quoteVolume)
                        ? (double)quoteVolume
                        : double.Parse(trade.QuotingVolume);
                }
                result.Add(item);
            }
            return result;
        }

        private bool IsBaseAssetMain(
            bool isOrderStraight,
            string assetPairId,
            string baseAssetId,
            string quotingAssetId)
        {
            bool isPairStartsWithBase = false;
            if (assetPairId.StartsWith(baseAssetId) && !assetPairId.EndsWith(baseAssetId)
                || assetPairId.EndsWith(quotingAssetId) && !assetPairId.StartsWith(quotingAssetId))
            {
                isPairStartsWithBase = true;
            }
            else if (assetPairId.StartsWith(quotingAssetId) && !assetPairId.EndsWith(quotingAssetId)
                || assetPairId.EndsWith(baseAssetId) && !assetPairId.StartsWith(baseAssetId))
            {
                isPairStartsWithBase = false;
            }
            else
            {
                var baseAsset = _assetsServiceWithCache.TryGetAssetAsync(baseAssetId).GetAwaiter().GetResult();
                bool found = false;
                if (baseAsset != null)
                {
                    var itemsToCheck = new HashSet<string>{ baseAsset.DisplayId, baseAsset.Name, baseAsset.Symbol };
                    foreach (var assetShortName in itemsToCheck)
                    {
                        if (assetPairId.StartsWith(assetShortName))
                        {
                            found = true;
                            isPairStartsWithBase = true;
                            break;
                        }
                        if (assetPairId.EndsWith(assetShortName))
                        {
                            found = true;
                            isPairStartsWithBase = false;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    var quoteAsset = _assetsServiceWithCache.TryGetAssetAsync(quotingAssetId).GetAwaiter().GetResult();
                    if (quoteAsset != null)
                    {
                        var itemsToCheck = new HashSet<string> { quoteAsset.DisplayId, quoteAsset.Name, quoteAsset.Symbol };
                        foreach (var assetShortName in itemsToCheck)
                        {
                            if (assetPairId.StartsWith(assetShortName))
                            {
                                isPairStartsWithBase = false;
                                break;
                            }
                            if (assetPairId.EndsWith(assetShortName))
                            {
                                isPairStartsWithBase = true;
                                break;
                            }
                        }
                    }
                }
            }

            return isOrderStraight ^ isPairStartsWithBase;
        }
    }
}
