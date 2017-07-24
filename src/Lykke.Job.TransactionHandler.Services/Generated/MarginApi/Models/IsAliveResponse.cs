// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{
    using System.Linq;

    public partial class IsAliveResponse
    {
        /// <summary>
        /// Initializes a new instance of the IsAliveResponse class.
        /// </summary>
        public IsAliveResponse() { }

        /// <summary>
        /// Initializes a new instance of the IsAliveResponse class.
        /// </summary>
        public IsAliveResponse(bool? matchingEngineAlive = default(bool?), bool? tradingEngineAlive = default(bool?), string version = default(string), string env = default(string))
        {
            MatchingEngineAlive = matchingEngineAlive;
            TradingEngineAlive = tradingEngineAlive;
            Version = version;
            Env = env;
        }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "matchingEngineAlive")]
        public bool? MatchingEngineAlive { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "tradingEngineAlive")]
        public bool? TradingEngineAlive { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "version")]
        public string Version { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "env")]
        public string Env { get; set; }

    }
}
