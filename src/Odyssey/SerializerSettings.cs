namespace Odyssey;

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

/// <summary>
///     Default <see cref="SerializerSettings"/> used across the project.
/// </summary>
public static class SerializerSettings
{
    /// <summary>
    ///     The default, and preferred, <see cref="SerializerSettings"/> to use.
    /// </summary>
    /// <remarks>
    ///     Changes to standard behavior;
    ///     - Ignore null values
    ///     - Convert enums as string
    ///     - Use snake_casing
    /// </remarks>
    public static readonly JsonSerializerSettings Default = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        Converters = new List<JsonConverter> { new StringEnumConverter() },
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        }
    };
}
