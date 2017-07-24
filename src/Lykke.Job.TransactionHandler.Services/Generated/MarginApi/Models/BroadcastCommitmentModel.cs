// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{
    using System.Linq;

    public partial class BroadcastCommitmentModel
    {
        /// <summary>
        /// Initializes a new instance of the BroadcastCommitmentModel class.
        /// </summary>
        public BroadcastCommitmentModel() { }

        /// <summary>
        /// Initializes a new instance of the BroadcastCommitmentModel class.
        /// </summary>
        public BroadcastCommitmentModel(string clientPubKey = default(string), string asset = default(string), string transaction = default(string))
        {
            ClientPubKey = clientPubKey;
            Asset = asset;
            Transaction = transaction;
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
        [Newtonsoft.Json.JsonProperty(PropertyName = "transaction")]
        public string Transaction { get; set; }

    }
}
