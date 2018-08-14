using System;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Lykke.Job.TransactionHandler.Core.Services.AppNotifications;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Services.Notifications
{
    public enum Device
    {
        Android, Ios
    }

    public interface IIosNotification { }

    public interface IAndroidNotification { }

    public class IosFields
    {
        [JsonProperty("alert")]
        public string Alert { get; set; }
        [JsonProperty("type")]
        public NotificationType Type { get; set; }
        [JsonProperty("sound")]
        public string Sound { get; set; } = "default";
    }

    public class AndroidPayloadFields
    {
        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("entity")]
        public string Entity { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class AssetsCreditedFieldsIos : IosFields
    {
        [JsonProperty("amount")]
        public double Amount { get; set; }
        [JsonProperty("assetId")]
        public string AssetId { get; set; }
    }

    public class AssetsCreditedFieldsAndroid : AndroidPayloadFields
    {
        public class BalanceItemModel
        {
            [JsonProperty("amount")]
            public double Amount { get; set; }
            [JsonProperty("assetId")]
            public string AssetId { get; set; }
        }

        [JsonProperty("balanceItem")]
        public BalanceItemModel BalanceItem { get; set; }
    }

    public class PushTxDialogFieldsIos : IosFields
    {
        [JsonProperty("amount")]
        public double Amount { get; set; }
        [JsonProperty("assetId")]
        public string AssetId { get; set; }
        [JsonProperty("addressFrom")]
        public string AddressFrom { get; set; }
        [JsonProperty("addressTo")]
        public string AddressTo { get; set; }
    }

    public class PushTxDialogFieldsAndroid : AndroidPayloadFields
    {
        public class PushDialogTxItemModel
        {
            [JsonProperty("amount")]
            public double Amount { get; set; }
            [JsonProperty("assetId")]
            public string AssetId { get; set; }
            [JsonProperty("addressFrom")]
            public string AddressFrom { get; set; }
            [JsonProperty("addressTo")]
            public string AddressTo { get; set; }
        }

        [JsonProperty("pushTxItem")]
        public PushDialogTxItemModel PushTxItem { get; set; }
    }

    public class IosNotification : IIosNotification
    {
        [JsonProperty("aps")]
        public IosFields Aps { get; set; }
    }

    public class AndoridPayloadNotification : IAndroidNotification
    {
        [JsonProperty("data")]
        public AndroidPayloadFields Data { get; set; }
    }

    public class DataNotificationFields : IosFields
    {
        [JsonProperty("content-available")]
        public int ContentAvailable { get; set; } = 1;
    }

    public class SrvAppNotifications : IAppNotifications
    {
        private readonly string _connectionString;
        private readonly string _hubName;

        public SrvAppNotifications(string connectionString, string hubName)
        {
            _connectionString = connectionString;
            _hubName = hubName;
        }

        public async Task SendDataNotificationToAllDevicesAsync(string[] notificationIds, NotificationType type,
            string entity, string id = "")
        {
            var apnsMessage = new IosNotification
            {
                Aps = new DataNotificationFields
                {
                    Type = type,
                    Sound = null
                }
            };

            var gcmMessage = new AndoridPayloadNotification
            {
                Data = new AndroidPayloadFields
                {
                    Entity = EventsAndEntities.GetEntity(type),
                    Event = EventsAndEntities.GetEvent(type),
                    Id = id
                }
            };

            await SendIosNotificationAsync(notificationIds, apnsMessage);
            await SendAndroidNotificationAsync(notificationIds, gcmMessage);
        }

        public async Task SendAssetsCreditedNotification(string[] notificationsIds, double amount, string assetId, string message)
        {
            var apnsMessage = new IosNotification
            {
                Aps = new AssetsCreditedFieldsIos
                {
                    Alert = message,
                    Amount = amount,
                    AssetId = assetId,
                    Type = NotificationType.AssetsCredited
                }
            };

            var gcmMessage = new AndoridPayloadNotification
            {
                Data = new AssetsCreditedFieldsAndroid
                {
                    Entity = EventsAndEntities.GetEntity(NotificationType.AssetsCredited),
                    Event = EventsAndEntities.GetEvent(NotificationType.AssetsCredited),
                    BalanceItem = new AssetsCreditedFieldsAndroid.BalanceItemModel
                    {
                        AssetId = assetId,
                        Amount = amount,
                    },
                    Message = message,
                }
            };

            await SendIosNotificationAsync(notificationsIds, apnsMessage);
            await SendAndroidNotificationAsync(notificationsIds, gcmMessage);
        }

        private async Task SendIosNotificationAsync(string[] notificationIds, IIosNotification notification)
        {
            await SendRawNotificationAsync(Device.Ios, notificationIds, notification.ToJson(ignoreNulls: true));
        }

        private async Task SendAndroidNotificationAsync(string[] notificationIds, IAndroidNotification notification)
        {
            await SendRawNotificationAsync(Device.Android, notificationIds, notification.ToJson(ignoreNulls: true));
        }

        private async Task SendRawNotificationAsync(Device device, string[] notificationIds, string payload)
        {
            try
            {
                notificationIds = notificationIds?.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                if (notificationIds != null && notificationIds.Any())
                {
                    var hub = CustomNotificationHubClient.CreateClientFromConnectionString(_connectionString, _hubName);

                    if (device == Device.Ios)
                        await hub.SendAppleNativeNotificationAsync(payload, notificationIds);
                    else
                        await hub.SendGcmNativeNotificationAsync(payload, notificationIds);
                }
            }
            catch (Exception)
            {
                //TODO: process exception
            }
        }

    }

}