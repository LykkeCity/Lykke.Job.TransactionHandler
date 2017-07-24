// Code generated by Microsoft (R) AutoRest Code Generator 1.0.1.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.EthereumCoreApi.Models
{
    using LkeServices.Generated;
    using LkeServices.Generated.EthereumCoreApi;
    using Newtonsoft.Json;
    using System.Linq;

    public partial class ExternalTokenModel
    {
        /// <summary>
        /// Initializes a new instance of the ExternalTokenModel class.
        /// </summary>
        public ExternalTokenModel()
        {
          CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the ExternalTokenModel class.
        /// </summary>
        public ExternalTokenModel(string id = default(string), string name = default(string), string contractAddress = default(string))
        {
            Id = id;
            Name = name;
            ContractAddress = contractAddress;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "contractAddress")]
        public string ContractAddress { get; set; }

    }
}
