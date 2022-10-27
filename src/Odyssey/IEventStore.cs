namespace Odyssey;

public interface IEventStore
{
    Task AppendToStream(string streamId, IReadOnlyList<EventData> events, ExpectedState expectedState, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EventData>> ReadStream(string streamId, Direction direction, StreamPosition position, CancellationToken cancellationToken);
}