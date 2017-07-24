// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{
    using System.Linq;

    public partial class AggregatedOrderBook
    {
        /// <summary>
        /// Initializes a new instance of the AggregatedOrderBook class.
        /// </summary>
        public AggregatedOrderBook() { }

        /// <summary>
        /// Initializes a new instance of the AggregatedOrderBook class.
        /// </summary>
        public AggregatedOrderBook(System.Collections.Generic.IList<AggregatedOrderBookItem> buy = default(System.Collections.Generic.IList<AggregatedOrderBookItem>), System.Collections.Generic.IList<AggregatedOrderBookItem> sell = default(System.Collections.Generic.IList<AggregatedOrderBookItem>))
        {
            Buy = buy;
            Sell = sell;
        }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "buy")]
        public System.Collections.Generic.IList<AggregatedOrderBookItem> Buy { get; set; }

        /// <summary>
        /// </summary>
        [Newtonsoft.Json.JsonProperty(PropertyName = "sell")]
        public System.Collections.Generic.IList<AggregatedOrderBookItem> Sell { get; set; }

    }
}
