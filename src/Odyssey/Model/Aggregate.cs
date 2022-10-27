namespace Odyssey.Model;

/// <summary>
/// Base class for aggregate objects
/// </summary>
/// <typeparam name="TId"></typeparam>
/// <remarks>
/// What is an aggregate? https://martinfowler.com/bliki/DDD_Aggregate.html
/// </remarks>
public abstract class Aggregate<TId> : IAggregate<TId>
{
    private readonly List<object> _events = new();

    public TId Id { get; protected set; } = default!;

    public long CurrentVersion { get; protected set; } = -1;
    public long LastVersion => CurrentVersion - _events.Count;

    protected abstract void When(object @event);

    protected void Raise(object @event)
    {
        Apply(@event);
        _events.Add(@event);
    }

    private void Apply(object @event)
    {
        When(@event);
        CurrentVersion++;
    }

    void IAggregate<TId>.Apply(object @event) => Apply(@event);
    IReadOnlyCollection<object> IAggregate<TId>.GetPendingEvents() => _events.AsReadOnly();
    void IAggregate<TId>.CommitPendingEvents() => _events.Clear();
}