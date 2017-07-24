// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{
    using System.Linq;

    public partial class Order
    {
        /// <summary>
        /// Initializes a new instance of the Order class.
        /// </summary>
        public Order() { }

        /// <summary>
        /// Initializes a new instance of the Order class.
        /// </summary>
        /// <param name="status">Possible values include:
        /// 'WaitingForExecution', 'Active', 'Closed', 'Rejected',
        /// 'Closing'</param>
        /// <param name="closeReason">Possible values include: 'None',
        /// 'Close', 'StopLoss', 'TakeProfit', 'StopOut', 'Canceled'</param>
        /// <param name="fillType">Possible values include: 'FillOrKill',
        /// 'PartialFill'</param>
        /// <param name="rejectReason">Possible values include: 'None',
        /// 'NoLiquidity', 'NotEnoughBalance', 'LeadToStopOut',
        /// 'AccountInvalidState', 'InvalidExpectedOpenPrice',
        /// 'InvalidVolume', 'InvalidTakeProfit', 'InvalidStoploss',
        /// 'InvalidInstrument', 'InvalidAccount', 'TradingConditionError',
        /// 'TechnicalError'</param>
        public Order(string id = default(string), string clientId = default(string), string accountId = default(string), string tradingConditionId = default(string), string accountAssetId = default(string), string openOrderbookId = default(string), string closeOrderbookId = default(string), string instrument = default(string), System.DateTime? createDate = default(System.DateTime?), System.DateTime? openDate = default(System.DateTime?), System.DateTime? closeDate = default(System.DateTime?), double? expectedOpenPrice = default(double?), double? openPrice = default(double?), double? closePrice = default(double?), double? quoteRate = default(double?), int? assetAccuracy = default(int?), double? volume = default(double?), double? takeProfit = default(double?), double? stopLoss = default(double?), double? openCommission = default(double?), double? closeCommission = default(double?), double? commissionLot = default(double?), double? swapCommission = default(double?), System.DateTime? startClosingDate = default(System.DateTime?), string status = default(string), string closeReason = default(string), string fillType = default(string), string rejectReason = default(string), string rejectReasonText = default(string), string comment = default(string), System.Collections.Generic.IList<MatchedOrder> matchedOrders = default(System.Collections.Generic.IList<MatchedOrder>), System.Collections.Generic.IList<MatchedOrder> matchedCloseOrders = default(System.Collections.Generic.IList<MatchedOrder>), double? pnL = default(double?))
        {
            Id = id;
            ClientId = clientId;
            AccountId = accountId;
            TradingConditionId = tradingConditionId;
            AccountAssetId = accountAssetId;
            OpenOrderbookId = openOrderbookId;
            CloseOrderbookId = closeOrderbookId;
            Instrument = instrument;
            CreateDate = createDate;
            OpenDate = openDate;
            CloseDate = closeDate;
            ExpectedOpenPrice = expectedOpenPrice;
            OpenPrice = openPrice;
            ClosePrice = closePrice;
            QuoteRate = quoteRate;
            AssetAccuracy = assetAccuracy;
            Volume = volume;
            TakeProfit = takeProfit;
            StopLoss = stopLoss;
            OpenCommission = openCommission;
            CloseCommission = closeCommission;
            CommissionLot = commissionLot;
            SwapCommission = swapCommission;
            StartClosingDate = startClosingDate;
            Status = status;
            CloseReason = closeReason;
            FillType = fillType;
            RejectReason = rejectReason;
            RejectReasonText = rejectReasonText;
            Comment = comment;
            MatchedOrders = matchedOrders;
            MatchedCloseOrders = matchedCloseOrders;
            PnL = pnL;
        }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "clientId")]
        public string ClientId { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "accountId")]
        public string AccountId { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "tradingConditionId")]
        public string TradingConditionId { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "accountAssetId")]
        public string AccountAssetId { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "openOrderbookId")]
        public string OpenOrderbookId { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "closeOrderbookId")]
        public string CloseOrderbookId { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "instrument")]
        public string Instrument { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "createDate")]
        public System.DateTime? CreateDate { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "openDate")]
        public System.DateTime? OpenDate { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "closeDate")]
        public System.DateTime? CloseDate { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "expectedOpenPrice")]
        public double? ExpectedOpenPrice { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "openPrice")]
        public double? OpenPrice { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "closePrice")]
        public double? ClosePrice { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "quoteRate")]
        public double? QuoteRate { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "assetAccuracy")]
        public int? AssetAccuracy { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "volume")]
        public double? Volume { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "takeProfit")]
        public double? TakeProfit { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "stopLoss")]
        public double? StopLoss { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "openCommission")]
        public double? OpenCommission { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "closeCommission")]
        public double? CloseCommission { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "commissionLot")]
        public double? CommissionLot { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "swapCommission")]
        public double? SwapCommission { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "startClosingDate")]
        public System.DateTime? StartClosingDate { get; set; }

        /// <summary>
        /// Gets or sets possible values include: 'WaitingForExecution',
        /// 'Active', 'Closed', 'Rejected', 'Closing'
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "status")]
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets possible values include: 'None', 'Close', 'StopLoss',
        /// 'TakeProfit', 'StopOut', 'Canceled'
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "closeReason")]
        public string CloseReason { get; set; }

        /// <summary>
        /// Gets or sets possible values include: 'FillOrKill', 'PartialFill'
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "fillType")]
        public string FillType { get; set; }

        /// <summary>
        /// Gets or sets possible values include: 'None', 'NoLiquidity',
        /// 'NotEnoughBalance', 'LeadToStopOut', 'AccountInvalidState',
        /// 'InvalidExpectedOpenPrice', 'InvalidVolume', 'InvalidTakeProfit',
        /// 'InvalidStoploss', 'InvalidInstrument', 'InvalidAccount',
        /// 'TradingConditionError', 'TechnicalError'
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "rejectReason")]
        public string RejectReason { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "rejectReasonText")]
        public string RejectReasonText { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "comment")]
        public string Comment { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "matchedOrders")]
        public System.Collections.Generic.IList<MatchedOrder> MatchedOrders { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "matchedCloseOrders")]
        public System.Collections.Generic.IList<MatchedOrder> MatchedCloseOrders { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "pnL")]
        public double? PnL { get; private set; }
    }
}
