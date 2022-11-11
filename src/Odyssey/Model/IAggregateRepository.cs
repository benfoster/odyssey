namespace Odyssey.Model;
using OneOf;

public interface IAggregateRepository<TId>
{
    Task<OneOf<T, AggregateNotFound>> GetById<T>(TId id, CancellationToken cancellationToken) where T : IAggregate<TId>, new();
    Task<OneOf<Success, UnexpectedStreamState>> Save(IAggregate<TId> aggregate, CancellationToken cancellationToken);
}