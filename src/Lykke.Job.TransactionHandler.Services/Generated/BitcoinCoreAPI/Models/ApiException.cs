// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.BitcoinCoreApi.Models
{
    public partial class ApiException
    {
        /// <summary>
        /// Initializes a new instance of the ApiException class.
        /// </summary>
        public ApiException() { }

        /// <summary>
        /// Initializes a new instance of the ApiException class.
        /// </summary>
        public ApiException(ApiError error = default(ApiError))
        {
            Error = error;
        }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "error")]
        public ApiError Error { get; set; }

    }
}
