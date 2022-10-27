namespace Odyssey.Model;

public interface IAggregateRepository<TId>
{
    Task<T> GetById<T>(TId id, CancellationToken cancellationToken) where T : IAggregate<TId>;
    Task Save(IAggregate<TId> aggregate, CancellationToken cancellationToken);
    Task<bool> Exists<T>(TId id, CancellationToken cancellationToken) where T : IAggregate<TId>;
}