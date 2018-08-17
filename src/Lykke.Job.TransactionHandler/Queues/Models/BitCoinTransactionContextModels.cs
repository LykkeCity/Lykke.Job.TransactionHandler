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
    }

}