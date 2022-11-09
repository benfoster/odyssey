namespace Odyssey.Model;

public interface IAggregateRepository<TId>
{
    Task<T> GetById<T>(TId id, CancellationToken cancellationToken) where T : IAggregate<TId>, new();
    Task Save(IAggregate<TId> aggregate, CancellationToken cancellationToken);
}