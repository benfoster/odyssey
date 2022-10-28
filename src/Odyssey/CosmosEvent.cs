namespace Odyssey;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed class CosmosEvent
{
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("stream_id")] // PK
    public string StreamId { get; set; } = null!;

    [JsonProperty("event_id")]
    public Guid EventId { get; set; }

    [JsonProperty("event_type")]
    public string EventType { get; set; } = null!;

    [JsonProperty("data")]
    public JObject Data { get; set; } = null!;

    [JsonProperty("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = null!;

    [JsonProperty("event_number")]
    public long EventNumber { get; set; }

    // https://learn.microsoft.com/en-us/azure/cosmos-db/account-databases-containers-items#properties-of-an-item
    [JsonProperty("_ts")] // Unix time
    public long? Timestamp { get; set; }

    public EventData ToEventData(JsonSerializer serializer)
    {
        if (!Metadata.TryGetValue(MetadataFields.ClrQualifiedType, out var clrTypeValue)
            || clrTypeValue is not string typeName)
        {
            throw new ArgumentException($"Item {Id} is missing the required {MetadataFields.ClrQualifiedType} metadata value");
        }

        // TODO implement type cache
        Type? clrType = Type.GetType(typeName);

        if (clrType is null)
        {
            throw new ArgumentException($"The type {typeName} cannot be resolved");
        }

        return new EventData(
            EventId,
            EventType,
            Data.ToObject(clrType, serializer),
            Metadata
        )
        {
            EventNumber = EventNumber
        };
    }

    public static CosmosEvent FromEventData(string streamId, long eventNumber, EventData @event, JsonSerializer serializer)
        => new()
        {
            Id = GenerateId(eventNumber, streamId),
            EventId = @event.Id,
            StreamId = streamId,
            EventType = @event.EventType,
            Data = JObject.FromObject(@event.Data, serializer),
            EventNumber = eventNumber,
            Metadata = @event.Metadata
        };

    public static string GenerateId(long eventNumber, string streamId) => $"{eventNumber}@{streamId}";
}