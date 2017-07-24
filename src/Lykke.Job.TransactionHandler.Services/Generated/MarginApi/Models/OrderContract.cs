// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{
    using System.Linq;

    public partial class OrderContract
    {
        /// <summary>
        /// Initializes a new instance of the OrderContract class.
        /// </summary>
        public OrderContract() { }

        /// <summary>
        /// Initializes a new instance of the OrderContract class.
        /// </summary>
        /// <param name="type">Possible values include: 'Buy', 'Sell'</param>
        /// <param name="status">Possible values include:
        /// 'WaitingForExecution', 'Active', 'Closed', 'Rejected',
        /// 'Closing'</param>
        /// <param name="closeReason">Possible values include: 'None',
        /// 'Close', 'StopLoss', 'TakeProfit', 'StopOut', 'Canceled'</param>
        /// <param name="rejectReason">Possible values include: 'None',
        /// 'NoLiquidity', 'NotEnoughBalance', 'LeadToStopOut',
        /// 'AccountInvalidState', 'InvalidExpectedOpenPrice',
        /// 'InvalidVolume', 'InvalidTakeProfit', 'InvalidStoploss',
        /// 'InvalidInstrument', 'InvalidAccount', 'TradingConditionError',
        /// 'TechnicalError'</param>
        public OrderContract(string id = default(string), string clientId = default(string), string accountId = default(string), string instrument = default(string), OrderDirection? type = default(OrderDirection?), OrderStatus? status = default(OrderStatus?), OrderCloseReason? closeReason = default(OrderCloseReason?), OrderRejectReason? rejectReason = default(OrderRejectReason?), string rejectReasonText = default(string), double? expectedOpenPrice = default(double?), double? openPrice = default(double?), double? closePrice = default(double?), System.DateTime? createDate = default(System.DateTime?), System.DateTime? openDate = default(System.DateTime?), System.DateTime? closeDate = default(System.DateTime?), double? volume = default(double?), double? matchedVolume = default(double?), double? matchedCloseVolume = default(double?), double? takeProfit = default(double?), double? stopLoss = default(double?), double? fpl = default(double?), double? pnL = default(double?), double? openCommission = default(double?), double? closeCommission = default(double?), double? swapCommission = default(double?), System.Collections.Generic.IList<MatchedOrderBackendContract> matchedOrders = default(System.Collections.Generic.IList<MatchedOrderBackendContract>), System.Collections.Generic.IList<MatchedOrderBackendContract> matchedCloseOrders = default(System.Collections.Generic.IList<MatchedOrderBackendContract>))
        {
            Id = id;
            ClientId = clientId;
            AccountId = accountId;
            Instrument = instrument;
            Type = type;
            Status = status;
            CloseReason = closeReason;
            RejectReason = rejectReason;
            RejectReasonText = rejectReasonText;
            ExpectedOpenPrice = expectedOpenPrice;
            OpenPrice = openPrice;
            ClosePrice = closePrice;
            CreateDate = createDate;
            OpenDate = openDate;
            CloseDate = closeDate;
            Volume = volume;
            MatchedVolume = matchedVolume;
            MatchedCloseVolume = matchedCloseVolume;
            TakeProfit = takeProfit;
            StopLoss = stopLoss;
            Fpl = fpl;
            PnL = pnL;
            OpenCommission = openCommission;
            CloseCommission = closeCommission;
            SwapCommission = swapCommission;
            MatchedOrders = matchedOrders;
            MatchedCloseOrders = matchedCloseOrders;
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
        [Newtonsoft.Json.JsonProperty(PropertyName = "instrument")]
        public string Instrument { get; set; }

        /// <summary>
        /// Gets or sets possible values include: 'Buy', 'Sell'
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "type")]
        public OrderDirection? Type { get; set; }

        /// <summary>
        /// Gets or sets possible values include: 'WaitingForExecution',
        /// 'Active', 'Closed', 'Rejected', 'Closing'
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "status")]
        public OrderStatus? Status { get; set; }

        /// <summary>
        /// Gets or sets possible values include: 'None', 'Close', 'StopLoss',
        /// 'TakeProfit', 'StopOut', 'Canceled'
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "closeReason")]
        public OrderCloseReason? CloseReason { get; set; }

        /// <summary>
        /// Gets or sets possible values include: 'None', 'NoLiquidity',
        /// 'NotEnoughBalance', 'LeadToStopOut', 'AccountInvalidState',
        /// 'InvalidExpectedOpenPrice', 'InvalidVolume', 'InvalidTakeProfit',
        /// 'InvalidStoploss', 'InvalidInstrument', 'InvalidAccount',
        /// 'TradingConditionError', 'TechnicalError'
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "rejectReason")]
        public OrderRejectReason? RejectReason { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "rejectReasonText")]
        public string RejectReasonText { get; set; }

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
        [Newtonsoft.Json.JsonProperty(PropertyName = "volume")]
        public double? Volume { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "matchedVolume")]
        public double? MatchedVolume { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "matchedCloseVolume")]
        public double? MatchedCloseVolume { get; set; }

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
        [Newtonsoft.Json.JsonProperty(PropertyName = "fpl")]
        public double? Fpl { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "pnL")]
        public double? PnL { get; set; }

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
        [Newtonsoft.Json.JsonProperty(PropertyName = "swapCommission")]
        public double? SwapCommission { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "matchedOrders")]
        public System.Collections.Generic.IList<MatchedOrderBackendContract> MatchedOrders { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "matchedCloseOrders")]
        public System.Collections.Generic.IList<MatchedOrderBackendContract> MatchedCloseOrders { get; set; }

    }
}
