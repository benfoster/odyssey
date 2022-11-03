namespace Odyssey;

public interface IEventStore
{
    Task Initialize(CancellationToken cancellationToken = default);
    Task AppendToStream(string streamId, IReadOnlyList<EventData> events, StreamState expectedState, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EventData>> ReadStream(string streamId, ReadDirection direction, StreamPosition position, CancellationToken cancellationToken = default);
}