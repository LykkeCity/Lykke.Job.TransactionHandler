// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{
    using System.Linq;

    public partial class MtResponseString
    {
        /// <summary>
        /// Initializes a new instance of the MtResponseString class.
        /// </summary>
        public MtResponseString() { }

        /// <summary>
        /// Initializes a new instance of the MtResponseString class.
        /// </summary>
        public MtResponseString(string result = default(string), string message = default(string))
        {
            Result = result;
            Message = message;
        }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "result")]
        public string Result { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

    }
}
