using System;
using System.Collections.Generic;

namespace Lykke.Job.TransactionHandler.Core.Domain.BitCoin
{
    public class GenerateWalletContextData : BaseContextData
    {
        public string ClientId { get; set; }

        public static GenerateWalletContextData Create(string clientId)
        {
            return new GenerateWalletContextData
            {
                ClientId = clientId
            };
        }
    }

    public class CashInOutContextData : BaseContextData
    {
        public string ClientId { get; set; }
        public string CashOperationId { get; set; }
    }

    public class IssueContextData : BaseContextData
    {
        public string ClientId { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }

        public string CashOperationId { get; set; }
    }

    public class CashOutContextData : BaseContextData
    {
        public string ClientId { get; set; }
        public string AssetId { get; set; }
        public string Address { get; set; }
        public double Amount { get; set; }
        public string CashOperationId { get; set; }
        public AdditionalData AddData { get; set; }

        public static CashOutContextData Create(string clientId, string assetId, string address, double amount,
            string cashOpId, AdditionalData additionalData = null)
        {
            return new CashOutContextData
            {
                ClientId = clientId,
                AssetId = assetId,
                Amount = amount,
                Address = address,
                CashOperationId = cashOpId,
                AddData = additionalData,
                SignsClientIds = new[] { clientId }
            };
        }

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

    public class TradeContextData : BaseContextData
    {
        public string MarketOrderId { get; set; }
        public DateTime DateTime { get; set; }

        public SwapContextData[] Swaps { get; set; }
    }

    public class SwapContextData : BaseContextData
    {
        public class TradeModel
        {
            public string ClientId { get; set; }
            public string TradeId { get; set; }
        }

        public TradeModel[] Trades { get; set; }
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
    }

    public class DirectSwapContextData : BaseContextData
    {
        public class PartyModel
        {
            public string ClientId { get; set; }
            public string AssetId { get; set; }
            public double Amount { get; set; }
        }

        public PartyModel FirstParty { get; set; }
        public PartyModel SecondParty { get; set; }
    }

    public enum TransferType
    {
        Common = 0,
        ToMarginAccount = 1,
        FromMarginAccount = 2,
        ToTrustedWallet = 3,
        FromTrustedWallet = 4
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

            public static TransferModel Create(string clientId, string operationId)
            {
                return new TransferModel
                {
                    ClientId = clientId,
                    OperationId = operationId
                };
            }
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
            public ConvertedOkEmailAction(string assetFromId, double price, double amountFrom, double amountLkk)
            {
                AssetFromId = assetFromId;
                Price = price;
                AmountFrom = amountFrom;
                AmountLkk = amountLkk;
            }

            public string AssetFromId { get; set; }
            public double Price { get; set; }
            public double AmountFrom { get; set; }
            public double AmountLkk { get; set; }
        }

        public class EmailAction
        {
            public EmailAction(string assetId, double amount)
            {
                AssetId = assetId;
                Amount = amount;
            }

            public string AssetId { get; set; }
            public double Amount { get; set; }
        }

        public class PushNotification
        {
            public PushNotification(string assetId, double amount)
            {
                AssetId = assetId;
                Amount = amount;
            }

            /// <summary>
            /// Id of credited asset
            /// </summary>
            public string AssetId { get; set; }

            public double Amount { get; set; }
        }

        public class GenerateTransferAction
        {
            public string DestClientId { get; set; }
            public string SourceClientId { get; set; }
            public double Amount { get; set; }
            public string AssetId { get; set; }
            public double Price { get; set; }
            public double AmountFrom { get; set; }
            public string FromAssetId { get; set; }
        }

        public class UpdateMarginBalance
        {
            public string AccountId { get; set; }
            public double Amount { get; set; }

            public UpdateMarginBalance(string account, double amount)
            {
                AccountId = account;
                Amount = amount;
            }
        }
        #endregion
    }

    public class RefundContextData : BaseContextData
    {
        public string ClientId { get; set; }
        public string SrcBlockchainHash { get; set; }
        public string OperationType { get; set; }
        public double? Amount { get; set; }
        public string AssetId { get; set; }
    }

    public class UncolorContextData : BaseContextData
    {
        public string ClientId { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }
        public string AddressFrom { get; set; }
        public string AddressTo { get; set; }

        public string CashOperationId { get; set; }
    }

    public class BaseContextData
    {
        public string[] SignsClientIds { get; set; }
    }

    public class TransferToMarginContextData : BaseContextData
    {
        public string ClientId { get; set; }
        public string MarginAccountId { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }

        public static TransferToMarginContextData Create(string clientId, string marginAccountId, string assetId, double amount)
        {
            return new TransferToMarginContextData
            {
                ClientId = clientId,
                AssetId = assetId,
                Amount = amount,
                MarginAccountId = marginAccountId,
                SignsClientIds = new[] { clientId }
            };
        }
    }

    public class RecieveFromMarginContextData : BaseContextData
    {
        public string ClientId { get; set; }
        public string MarginAccountId { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }

        public static RecieveFromMarginContextData Create(string clientId, string marginAccountId, string assetId, double amount)
        {
            return new RecieveFromMarginContextData
            {
                ClientId = clientId,
                AssetId = assetId,
                Amount = amount,
                MarginAccountId = marginAccountId,
                SignsClientIds = new[] { clientId }
            };
        }
    }
}