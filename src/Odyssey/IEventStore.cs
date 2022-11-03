namespace Odyssey;

public interface IEventStore
{
    Task Initialize(CancellationToken cancellationToken);
    Task AppendToStream(string streamId, IReadOnlyList<EventData> events, StreamState expectedState, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EventData>> ReadStream(string streamId, ReadDirection direction, StreamPosition position, CancellationToken cancellationToken);
}