// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{
    using System.Linq;

    public partial class InitAccountsRequest
    {
        /// <summary>
        /// Initializes a new instance of the InitAccountsRequest class.
        /// </summary>
        public InitAccountsRequest() { }

        /// <summary>
        /// Initializes a new instance of the InitAccountsRequest class.
        /// </summary>
        public InitAccountsRequest(string clientId = default(string), string tradingConditionsId = default(string))
        {
            ClientId = clientId;
            TradingConditionsId = tradingConditionsId;
        }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "clientId")]
        public string ClientId { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "tradingConditionsId")]
        public string TradingConditionsId { get; set; }

    }
}
