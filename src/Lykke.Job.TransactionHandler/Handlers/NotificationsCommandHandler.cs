using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.LimitTrades;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Core.Services.AppNotifications;
using Lykke.Job.TransactionHandler.Resources;
using Lykke.Service.Assets.Client;
using Lykke.Service.ClientAccount.Client;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class NotificationsCommandHandler
    {
        private readonly ILog _log;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IClientSettingsRepository _clientSettingsRepository;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IAppNotifications _appNotifications;

        public NotificationsCommandHandler(
            [NotNull] ILog log,
            IAssetsServiceWithCache assetsServiceWithCache,
            IClientSettingsRepository clientSettingsRepository,
            IClientAccountClient clientAccountClient,
            IAppNotifications appNotifications)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _assetsServiceWithCache = assetsServiceWithCache;
            _clientSettingsRepository = clientSettingsRepository;
            _clientAccountClient = clientAccountClient;
            _appNotifications = appNotifications;
        }

        [UsedImplicitly]
        public async Task<CommandHandlingResult> Handle(LimitTradeNotifySendCommand command)
        {
            _log.WriteInfo(nameof(NotificationsCommandHandler), JsonConvert.SerializeObject(command, Formatting.Indented), "LimitTradeNotifySendCommand");

            var order = command.LimitOrder.Order;
            var aggregated = command.Aggregated ?? new List<AggregatedTransfer>();
            var status = (OrderStatus)Enum.Parse(typeof(OrderStatus), order.Status);

            var clientId = order.ClientId;
            var type = order.Volume > 0 ? OrderType.Buy : OrderType.Sell;
            var typeString = type.ToString().ToLower();
            var assetPair = await _assetsServiceWithCache.TryGetAssetPairAsync(order.AssetPairId); 

             var receivedAsset = type == OrderType.Buy ? assetPair.BaseAssetId : assetPair.QuotingAssetId;
            var receivedAssetEntity = await _assetsServiceWithCache.TryGetAssetAsync(receivedAsset);

            var priceAsset = await _assetsServiceWithCache.TryGetAssetAsync(assetPair.QuotingAssetId);

            var prevRemainingVolumeNotEmpty = command.PrevRemainingVolume.HasValue
                                              && Math.Abs(command.PrevRemainingVolume.Value) > 0.0
                                              && Math.Abs(command.PrevRemainingVolume.Value) >= assetPair.MinVolume;
            var remainingVolume = (decimal)Math.Abs(prevRemainingVolumeNotEmpty ? command.PrevRemainingVolume.Value : order.Volume);
            var executedSum = Math.Abs(aggregated.Where(x => x.ClientId == clientId && x.AssetId == receivedAsset)
                                .Select(x => x.Amount)
                                .DefaultIfEmpty(0)
                                .Sum()).TruncateDecimalPlaces(receivedAssetEntity.Accuracy);

            string msg;

            switch (status)
            {
                // already handled in wallet api
                case OrderStatus.InOrderBook:
                case OrderStatus.Cancelled:
                case OrderStatus.NoLiquidity:
                case OrderStatus.NotEnoughFunds:
                case OrderStatus.ReservedVolumeGreaterThanBalance:
                case OrderStatus.UnknownAsset:
                case OrderStatus.LeadToNegativeSpread:
                case OrderStatus.InvalidFee:
                case OrderStatus.TooSmallVolume:
                    return CommandHandlingResult.Ok();
                case OrderStatus.Processing:
                    msg = string.Format(TextResources.LimitOrderPartiallyExecuted, typeString, order.AssetPairId, remainingVolume, order.Price, priceAsset.DisplayId, executedSum, receivedAssetEntity.DisplayId);
                    break;
                case OrderStatus.Matched:
                    msg = string.Format(TextResources.LimitOrderExecuted, typeString, order.AssetPairId, remainingVolume, order.Price, priceAsset.DisplayId, executedSum, receivedAssetEntity.DisplayId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(OrderStatus));
            }

            var pushSettings = await _clientSettingsRepository.GetSettings<PushNotificationsSettings>(clientId);
            if (pushSettings.Enabled)
            {
                var clientAcc = await _clientAccountClient.GetByIdAsync(clientId);

                await _appNotifications.SendLimitOrderNotification(new[] { clientAcc.NotificationsId }, msg, type, status);

                _log.WriteInfo(nameof(NotificationsCommandHandler), JsonConvert.SerializeObject(command, Formatting.Indented), $"Client {clientId}. Order status: {status}. Push message: {msg}");
            }

            return CommandHandlingResult.Ok();
        }
    }
}