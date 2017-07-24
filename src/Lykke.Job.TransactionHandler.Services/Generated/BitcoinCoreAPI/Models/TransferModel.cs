// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.BitcoinCoreApi.Models
{
    public partial class TransferModel
    {
        /// <summary>
        /// Initializes a new instance of the TransferModel class.
        /// </summary>
        public TransferModel() { }

        /// <summary>
        /// Initializes a new instance of the TransferModel class.
        /// </summary>
        public TransferModel(string clientPubKey = default(string), decimal? amount = default(decimal?), string asset = default(string), string clientPrevPrivateKey = default(string), bool? requiredOperation = default(bool?), System.Guid? transferId = default(System.Guid?))
        {
            ClientPubKey = clientPubKey;
            Amount = amount;
            Asset = asset;
            ClientPrevPrivateKey = clientPrevPrivateKey;
            RequiredOperation = requiredOperation;
            TransferId = transferId;
        }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "clientPubKey")]
        public string ClientPubKey { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "amount")]
        public decimal? Amount { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "asset")]
        public string Asset { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "clientPrevPrivateKey")]
        public string ClientPrevPrivateKey { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "requiredOperation")]
        public bool? RequiredOperation { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "transferId")]
        public System.Guid? TransferId { get; set; }

    }
}
