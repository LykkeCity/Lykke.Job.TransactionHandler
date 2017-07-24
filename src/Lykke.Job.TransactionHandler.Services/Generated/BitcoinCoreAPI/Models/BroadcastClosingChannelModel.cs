// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.BitcoinCoreApi.Models
{
    public partial class BroadcastClosingChannelModel
    {
        /// <summary>
        /// Initializes a new instance of the BroadcastClosingChannelModel
        /// class.
        /// </summary>
        public BroadcastClosingChannelModel() { }

        /// <summary>
        /// Initializes a new instance of the BroadcastClosingChannelModel
        /// class.
        /// </summary>
        public BroadcastClosingChannelModel(string clientPubKey = default(string), string asset = default(string), string signedByClientTransaction = default(string), System.Guid? notifyTxId = default(System.Guid?))
        {
            ClientPubKey = clientPubKey;
            Asset = asset;
            SignedByClientTransaction = signedByClientTransaction;
            NotifyTxId = notifyTxId;
        }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "clientPubKey")]
        public string ClientPubKey { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "asset")]
        public string Asset { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "signedByClientTransaction")]
        public string SignedByClientTransaction { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "notifyTxId")]
        public System.Guid? NotifyTxId { get; set; }

    }
}
