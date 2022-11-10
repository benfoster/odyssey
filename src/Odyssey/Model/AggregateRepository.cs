namespace Odyssey.Model;

using System.Threading.Tasks;
using O9d.Guard;
using OneOf;

public sealed class AggregateRepository<TId> : IAggregateRepository<TId>
{
    private readonly IEventStore _eventStore;

    public AggregateRepository(IEventStore eventStore)
    {
        _eventStore = eventStore.NotNull();
    }

    public async Task<OneOf<T, AggregateNotFound>> GetById<T>(TId id, CancellationToken cancellationToken = default) where T : IAggregate<TId>, new()
    {
        string streamId = id?.ToString() ?? throw new ArgumentException("The string representation of the aggregate ID cannot be null", nameof(id));

        var aggregate = new T();

        IReadOnlyCollection<EventData> events
            = await _eventStore.ReadStream(streamId, ReadDirection.Forwards, StreamPosition.Start, cancellationToken);

        if (events.Count == 0)
        {
            return AggregateNotFound.Instance;
        }

        foreach (var @event in events)
        {
            aggregate.Apply(@event.Data);
        }

        return aggregate;
    }

    public async Task<OneOf<Success, UnexpectedStreamState>> Save(IAggregate<TId> aggregate, CancellationToken cancellationToken = default)
    {
        aggregate.NotNull();
        string streamId = aggregate.Id?.ToString() ?? throw new ArgumentException("The aggregate ID cannot be null", nameof(aggregate));

        var aggregateEvents = aggregate.GetPendingEvents();
        if (aggregateEvents.Count == 0)
        {
            aggregate.CommitPendingEvents();
            return Success.Instance;
        }

        var eventsToStore = new List<EventData>();
        foreach (var @event in aggregateEvents)
        {
            eventsToStore.Add(CreateEventData(@event));
        }

        var result = await _eventStore.AppendToStream(streamId, eventsToStore.AsReadOnly(), StreamState.AtVersion(aggregate.LastVersion), cancellationToken);
        aggregate.CommitPendingEvents();

        return result;
    }

    // TODO allow metadata to be provided
    // Snake case event by default?
    private static EventData CreateEventData(object @event)
        => new(Guid.NewGuid(), @event.GetType().Name, @event);
}
