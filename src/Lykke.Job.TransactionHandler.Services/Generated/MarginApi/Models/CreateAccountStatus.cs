// Code generated by Microsoft (R) AutoRest Code Generator 0.17.0.0
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.

namespace LkeServices.Generated.MarginApi.Models
{

    /// <summary>
    /// Defines values for CreateAccountStatus.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum CreateAccountStatus
    {
        [System.Runtime.Serialization.EnumMember(Value = "Available")]
        Available,
        [System.Runtime.Serialization.EnumMember(Value = "Created")]
        Created,
        [System.Runtime.Serialization.EnumMember(Value = "Error")]
        Error
    }
}
