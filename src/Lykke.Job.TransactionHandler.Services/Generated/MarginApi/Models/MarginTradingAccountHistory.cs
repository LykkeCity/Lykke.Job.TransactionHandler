// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{
    using System.Linq;

    public partial class MarginTradingAccountHistory
    {
        /// <summary>
        /// Initializes a new instance of the MarginTradingAccountHistory
        /// class.
        /// </summary>
        public MarginTradingAccountHistory() { }

        /// <summary>
        /// Initializes a new instance of the MarginTradingAccountHistory
        /// class.
        /// </summary>
        /// <param name="type">Possible values include: 'Deposit', 'Withdraw',
        /// 'OrderClosed'</param>
        public MarginTradingAccountHistory(string id = default(string), System.DateTime? date = default(System.DateTime?), string accountId = default(string), string clientId = default(string), double? amount = default(double?), double? balance = default(double?), string comment = default(string), string type = default(string))
        {
            Id = id;
            Date = date;
            AccountId = accountId;
            ClientId = clientId;
            Amount = amount;
            Balance = balance;
            Comment = comment;
            Type = type;
        }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "date")]
        public System.DateTime? Date { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "accountId")]
        public string AccountId { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "clientId")]
        public string ClientId { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "amount")]
        public double? Amount { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "balance")]
        public double? Balance { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "comment")]
        public string Comment { get; set; }

        /// <summary>
        /// Gets or sets possible values include: 'Deposit', 'Withdraw',
        /// 'OrderClosed'
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

    }
}
