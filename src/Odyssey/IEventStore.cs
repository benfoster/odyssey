namespace Odyssey;

using OneOf;

public interface IEventStore
{
    Task Initialize(CancellationToken cancellationToken = default);
    Task<OneOf<Success, UnexpectedStreamState>> AppendToStream(string streamId, IReadOnlyList<EventData> events, StreamState expectedState, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EventData>> ReadStream(string streamId, ReadDirection direction, StreamPosition position, CancellationToken cancellationToken = default);
}