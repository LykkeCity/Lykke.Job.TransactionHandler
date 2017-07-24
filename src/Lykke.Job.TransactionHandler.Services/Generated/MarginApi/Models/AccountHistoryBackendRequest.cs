// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{
    using System.Linq;

    public partial class AccountHistoryBackendRequest
    {
        /// <summary>
        /// Initializes a new instance of the AccountHistoryBackendRequest
        /// class.
        /// </summary>
        public AccountHistoryBackendRequest() { }

        /// <summary>
        /// Initializes a new instance of the AccountHistoryBackendRequest
        /// class.
        /// </summary>
        public AccountHistoryBackendRequest(string clientId = default(string), string accountId = default(string), System.DateTime? fromProperty = default(System.DateTime?), System.DateTime? to = default(System.DateTime?))
        {
            ClientId = clientId;
            AccountId = accountId;
            FromProperty = fromProperty;
            To = to;
        }

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
        [Newtonsoft.Json.JsonProperty(PropertyName = "from")]
        public System.DateTime? FromProperty { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "to")]
        public System.DateTime? To { get; set; }

    }
}
