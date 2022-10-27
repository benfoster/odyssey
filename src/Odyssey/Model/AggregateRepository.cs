namespace Odyssey.Model;

using System.Threading.Tasks;
using O9d.Guard;

public sealed class AggregateRepository<TId> : IAggregateRepository<TId>
{
    private readonly IEventStore _eventStore;

    public AggregateRepository(IEventStore eventStore)
    {
        _eventStore = eventStore.NotNull();
    }

    public Task<bool> Exists<T>(TId id, CancellationToken cancellationToken = default) where T : IAggregate<TId>
    {
        throw new NotImplementedException();
    }

    public async Task<T> GetById<T>(TId id, CancellationToken cancellationToken = default) where T : IAggregate<TId>
    {
        string streamId = id?.ToString() ?? throw new ArgumentException("The string representation of the aggregate ID cannot be null", nameof(id));

        var aggregate = Activator.CreateInstance<T>(); // TODO alternative to reflection

        IReadOnlyCollection<EventData> events
            = await _eventStore.ReadStream(streamId, Direction.Forwards, StreamPosition.Start, cancellationToken);

        foreach (var @event in events)
        {
            aggregate.Apply(@event.Data);
        }

        return aggregate;
    }

    public async Task Save(IAggregate<TId> aggregate, CancellationToken cancellationToken = default)
    {
        aggregate.NotNull();
        string streamId = aggregate.Id?.ToString() ?? throw new ArgumentException("The aggregate ID cannot be null", nameof(aggregate));

        var aggregateEvents = aggregate.GetPendingEvents();
        if (aggregateEvents.Count == 0)
        {
            aggregate.CommitPendingEvents();
            return;
        }

        var eventsToStore = new List<EventData>();
        foreach (var @event in aggregateEvents)
        {
            eventsToStore.Add(CreateEventData(@event));
        }

        await _eventStore.AppendToStream(streamId, eventsToStore.AsReadOnly(), ExpectedState.AtVersion(aggregate.LastVersion), cancellationToken);
        aggregate.CommitPendingEvents();
    }

    // TODO allow metadata to be provided
    // Snake case event by default?
    private static EventData CreateEventData(object @event)
        => new(Guid.NewGuid(), @event.GetType().Name, @event);
}
