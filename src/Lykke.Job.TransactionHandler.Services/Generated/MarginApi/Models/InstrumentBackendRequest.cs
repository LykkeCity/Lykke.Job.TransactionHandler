// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{
    using System.Linq;

    public partial class InstrumentBackendRequest
    {
        /// <summary>
        /// Initializes a new instance of the InstrumentBackendRequest class.
        /// </summary>
        public InstrumentBackendRequest() { }

        /// <summary>
        /// Initializes a new instance of the InstrumentBackendRequest class.
        /// </summary>
        public InstrumentBackendRequest(string instrument = default(string))
        {
            Instrument = instrument;
        }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "instrument")]
        public string Instrument { get; set; }

    }
}
