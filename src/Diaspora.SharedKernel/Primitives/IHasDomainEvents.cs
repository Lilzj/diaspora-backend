namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// Implemented by entities that record domain events. Allows infrastructure to
/// discover pending events without knowing the entity's identifier type.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
