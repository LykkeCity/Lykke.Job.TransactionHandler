using System.Collections.Generic;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Sagas.Services;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    public class IssueContextData : BaseContextData
    {
        public string ClientId { get; set; }
        public string AssetId { get; set; }
        public decimal Amount { get; set; }

        public string CashOperationId { get; set; }
    }

    public class CashOutContextData : BaseContextData
    {
        public string ClientId { get; set; }
        public string AssetId { get; set; }
        public string Address { get; set; }
        public decimal Amount { get; set; }
        public string CashOperationId { get; set; }
        public AdditionalData AddData { get; set; }

        #region Additional data

        public class AdditionalData
        {
            public SwiftData SwiftData { get; set; }
            public ForwardWithdrawal ForwardWithdrawal { get; set; }
        }

        public class SwiftData
        {
            public string CashOutRequestId { get; set; }
        }

        public class ForwardWithdrawal
        {
            public string Id { get; set; }
        }

        #endregion

    }

    public class SwapOffchainContextData : BaseContextData
    {
        public class Operation
        {
            public string ClientId { get; set; }
            public decimal Amount { get; set; }
            public string AssetId { get; set; }
            public string TransactionId { get; set; }
            public string ClientTradeId { get; set; }
        }

        public List<Operation> Operations { get; set; } = new List<Operation>();
        public AggregatedTransfer SellTransfer { get; set; }
        public AggregatedTransfer BuyTransfer { get; set; }
        public ClientTrade[] ClientTrades { get; set; }
        public bool IsTrustedClient { get; set; }
        public TradeQueueItem.MarketOrder Order { get; set; }
        public List<TradeQueueItem.TradeInfo> Trades { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public OperationStatus Status { get; set; }
    }

    public class AggregatedTransfer
    {
        public string TransferId { get; set; }
        public decimal Amount { get; set; }
        public Asset Asset { get; set; }
    }

    public enum TransferType
    {
        Common = 0,
        ToMarginAccount = 1,
        FromMarginAccount = 2,
        ToTrustedWallet = 3,
        FromTrustedWallet = 4,
        BetweenTrusted = 5
    }

    public class TransferContextData : BaseContextData
    {
        public string SourceClient { get; set; }
        public TransferType TransferType { get; set; }

        public class TransferModel
        {
            public string ClientId { get; set; }
            public string OperationId { get; set; }
            public AdditionalActions Actions { get; set; }
        }

        public TransferModel[] Transfers { get; set; }


        public static TransferContextData Create(string srcClientId, params TransferModel[] transfers)
        {
            return new TransferContextData
            {
                SourceClient = srcClientId,
                Transfers = transfers,
                SignsClientIds = new[] { srcClientId }
            };
        }

        #region Actions

        public class AdditionalActions
        {
            /// <summary>
            /// If set, then transfer complete email with conversion to LKK will be sent on successful resonse from queue
            /// </summary>
            public ConvertedOkEmailAction CashInConvertedOkEmail { get; set; }

            /// <summary>
            /// If set, then push notification will be sent when transfer detected and confirmed
            /// </summary>
            public PushNotification PushNotification { get; set; }

            /// <summary>
            /// If set, transfer complete email will be sent
            /// </summary>
            public EmailAction SendTransferEmail { get; set; }

            /// <summary>
            /// If set, then another transfer will be generated on successful resonse from queue
            /// </summary>
            public GenerateTransferAction GenerateTransferAction { get; set; }

            /// <summary>
            /// For margin wallet deposit
            /// </summary>
            public UpdateMarginBalance UpdateMarginBalance { get; set; }
        }

        public class ConvertedOkEmailAction
        {
            public ConvertedOkEmailAction(string assetFromId, decimal price, decimal amountFrom, decimal amountLkk)
            {
                AssetFromId = assetFromId;
                Price = price;
                AmountFrom = amountFrom;
                AmountLkk = amountLkk;
            }

            public string AssetFromId { get; set; }
            public decimal Price { get; set; }
            public decimal AmountFrom { get; set; }
            public decimal AmountLkk { get; set; }
        }

        public class EmailAction
        {
            public EmailAction(string assetId, decimal amount)
            {
                AssetId = assetId;
                Amount = amount;
            }

            public string AssetId { get; set; }
            public decimal Amount { get; set; }
        }

        public class PushNotification
        {
            public PushNotification(string assetId, decimal amount)
            {
                AssetId = assetId;
                Amount = amount;
            }

            /// <summary>
            /// Id of credited asset
            /// </summary>
            public string AssetId { get; set; }

            public decimal Amount { get; set; }
        }

        public class GenerateTransferAction
        {
            public string DestClientId { get; set; }
            public string SourceClientId { get; set; }
            public decimal Amount { get; set; }
            public string AssetId { get; set; }
            public decimal Price { get; set; }
            public decimal AmountFrom { get; set; }
            public string FromAssetId { get; set; }
        }

        public class UpdateMarginBalance
        {
            public string AccountId { get; set; }
            public decimal Amount { get; set; }

            public UpdateMarginBalance(string account, decimal amount)
            {
                AccountId = account;
                Amount = amount;
            }
        }
        #endregion
    }

}