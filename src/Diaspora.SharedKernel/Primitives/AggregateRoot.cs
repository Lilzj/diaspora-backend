namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// Base class for aggregate roots. Aggregates are the consistency boundary
/// for domain operations and the unit of persistence.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    protected AggregateRoot(TId id) : base(id) { }

    // EF Core constructor
    protected AggregateRoot() { }

    /// <summary>
    /// Raises a domain event on this aggregate.
    /// Events are dispatched after the aggregate is persisted.
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        AddDomainEvent(domainEvent);
    }
}
