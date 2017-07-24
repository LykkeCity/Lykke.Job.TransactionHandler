// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{
    using System.Linq;

    public partial class ChangeOrderLimitRequest
    {
        /// <summary>
        /// Initializes a new instance of the ChangeOrderLimitRequest class.
        /// </summary>
        public ChangeOrderLimitRequest() { }

        /// <summary>
        /// Initializes a new instance of the ChangeOrderLimitRequest class.
        /// </summary>
        public ChangeOrderLimitRequest(string orderId = default(string), double? takeProfit = default(double?), double? stopLoss = default(double?), double? volume = default(double?), double? expectedOpenPrice = default(double?), string token = default(string), string clientId = default(string))
        {
            OrderId = orderId;
            TakeProfit = takeProfit;
            StopLoss = stopLoss;
            Volume = volume;
            ExpectedOpenPrice = expectedOpenPrice;
            Token = token;
            ClientId = clientId;
        }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "orderId")]
        public string OrderId { get; set; }

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
        [Newtonsoft.Json.JsonProperty(PropertyName = "volume")]
        public double? Volume { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "expectedOpenPrice")]
        public double? ExpectedOpenPrice { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "token")]
        public string Token { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "clientId")]
        public string ClientId { get; set; }

    }
}
