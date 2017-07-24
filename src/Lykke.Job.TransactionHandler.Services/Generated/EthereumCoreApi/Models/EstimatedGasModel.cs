// Code generated by Microsoft (R) AutoRest Code Generator 1.0.1.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.EthereumCoreApi.Models
{
    using LkeServices.Generated;
    using LkeServices.Generated.EthereumCoreApi;
    using Newtonsoft.Json;
    using System.Linq;

    public partial class EstimatedGasModel
    {
        /// <summary>
        /// Initializes a new instance of the EstimatedGasModel class.
        /// </summary>
        public EstimatedGasModel()
        {
          CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the EstimatedGasModel class.
        /// </summary>
        public EstimatedGasModel(string estimatedGas = default(string), bool? isAllowed = default(bool?))
        {
            EstimatedGas = estimatedGas;
            IsAllowed = isAllowed;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "estimatedGas")]
        public string EstimatedGas { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "isAllowed")]
        public bool? IsAllowed { get; set; }

    }
}
