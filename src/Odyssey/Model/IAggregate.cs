namespace Odyssey.Model;

public interface IAggregate<TId>
{
    TId Id { get; }

    /// <summary>
    /// Gets the current version of the aggregate
    /// </summary>
    long CurrentVersion { get; }

    /// <summary>
    /// Gets the last version of the aggregate at the point it was persisted, before any pending events are applied
    /// </summary>
    long LastVersion { get; }

    IReadOnlyCollection<object> GetPendingEvents();

    void CommitPendingEvents();

    void Apply(object @event);
}