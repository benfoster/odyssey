namespace Odyssey.Model;

using OneOf;
using OneOf.Types;

public interface IAggregateRepository<TId>
{
    Task<OneOf<T, NotFound>> GetById<T>(TId id, CancellationToken cancellationToken) where T : IAggregate<TId>, new();
    Task Save(IAggregate<TId> aggregate, CancellationToken cancellationToken);
}