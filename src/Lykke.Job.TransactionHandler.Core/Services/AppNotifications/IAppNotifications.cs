using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Services.AppNotifications
{
    public enum NotificationType
    {
        Info = 0,
        KycSucceess = 1,
        KycRestrictedArea = 2,
        KycNeedToFillDocuments = 3,
        TransctionFailed = 4,
        TransactionConfirmed = 5,
        AssetsCredited = 6,
        BackupWarning = 7,
        EthNeedTransactionSign = 8,
        PositionOpened = 9,
        PositionClosed = 10,
        MarginCall = 11,
        OffchainRequest = 12,
        NeedTransactionSign = 13,
        PushTxDialog = 14,
        LimitOrderEvent = 15
    }

    public static class EventsAndEntities
    {
        // ReSharper disable once InconsistentNaming
        public const string KYC = "KYC";
        public const string TradingWallet = "TradingWallet";
        public const string MarginWallet = "MarginWallet";

        public const string Ok = "Ok";
        public const string RestrictedArea = "RestrictedArea";
        public const string NeedToFillData = "NeedToFillData";
        public const string TxFailed = "TxFailed";
        public const string TxConfirmed = "TxConfirmed";
        public const string DepositCompleted = "DepositCompleted";
        public const string PositionOpened = "PositionOpened";
        public const string PositionClosed = "PositionClosed";
        public const string MarginCall = "MarginCall";
        public const string NeedTransactionSign = "NeedTransactionSign";
        public const string PushTxDialog = "PushTxDialog";

        public const string Offchain = "Offchain";
        public const string OffchainRequest = "OffchainRequest";

        public const string LimitOrderEvent = "LimitOrderEvent";

        public static string GetEntity(NotificationType notification)
        {
            switch (notification)
            {
                case NotificationType.KycSucceess:
                case NotificationType.KycRestrictedArea:
                case NotificationType.KycNeedToFillDocuments:
                    return KYC;
                case NotificationType.TransactionConfirmed:
                case NotificationType.TransctionFailed:
                case NotificationType.AssetsCredited:
                case NotificationType.PushTxDialog:
                    return TradingWallet;

                case NotificationType.PositionOpened:
                case NotificationType.PositionClosed:
                case NotificationType.MarginCall:
                    return MarginWallet;
                case NotificationType.NeedTransactionSign:
                    return NeedTransactionSign;
                case NotificationType.OffchainRequest:
                    return Offchain;
                case NotificationType.LimitOrderEvent:
                    return LimitOrderEvent;
                default:
                    throw new ArgumentException("Unknown notification");
            }
        }

        public static string GetEvent(NotificationType notification)
        {
            switch (notification)
            {
                case NotificationType.KycSucceess:
                    return Ok;
                case NotificationType.KycRestrictedArea:
                    return RestrictedArea;
                case NotificationType.KycNeedToFillDocuments:
                    return NeedToFillData;
                case NotificationType.TransactionConfirmed:
                    return TxConfirmed;
                case NotificationType.TransctionFailed:
                    return TxFailed;
                case NotificationType.AssetsCredited:
                    return DepositCompleted;
                case NotificationType.PositionOpened:
                    return PositionOpened;
                case NotificationType.PositionClosed:
                    return PositionClosed;
                case NotificationType.MarginCall:
                    return MarginCall;
                case NotificationType.OffchainRequest:
                    return OffchainRequest;
                case NotificationType.NeedTransactionSign:
                    return NeedTransactionSign;
                case NotificationType.PushTxDialog:
                    return PushTxDialog;
                case NotificationType.LimitOrderEvent:
                    return LimitOrderEvent;
                default:
                    throw new ArgumentException("Unknown notification");
            }
        }
    }

    public interface IAppNotifications
    {
        Task SendDataNotificationToAllDevicesAsync(string[] notificationIds, NotificationType type, string entity, string id = "");

        Task SendAssetsCreditedNotification(string[] notificationsIds, double amount, string assetId, string message);
    }
}