namespace Odyssey;

using O9d.Guard;

/// <summary>
/// Represents an event to be written.
/// </summary>
public sealed class EventData
{
    public EventData(
        Guid id,
        string eventType,
        object data,
        Dictionary<string, object>? metadata = null)
    {
        Id = id;
        EventType = eventType.NotNullOrWhiteSpace();
        Data = data.NotNull();
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the unique identifier of the event. Used for idempotent writes.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the normalised, human readable type of event
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// Gets the object representing the event's data
    /// </summary>
    public object Data { get; }

    /// <summary>
    /// Gets the metadata of the event
    /// </summary>
    public Dictionary<string, object>? Metadata { get; }
}