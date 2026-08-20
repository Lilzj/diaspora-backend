using Diaspora.Modules.CommunityLife.Domain.Identifiers;

namespace Diaspora.Modules.CommunityLife.Domain.Catalog;

/// <summary>How the module reads and writes the event-category catalogue.</summary>
public interface IEventCategoryRepository
{
    Task<EventCategory?> GetAsync(
        EventCategoryId id, CancellationToken cancellationToken = default);

    Task<EventCategory?> FindByKeyAsync(
        string? key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventCategory>> ListAsync(
        bool includeRetired = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every category in a named search facet — religious, cultural, business, student.
    /// <para>
    /// The search layer resolves a facet to its category keys once per query rather than joining
    /// the catalogue into the event query, which keeps the hot path a single indexed predicate.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<EventCategory>> ListByFacetAsync(
        string facet, CancellationToken cancellationToken = default);

    Task AddAsync(EventCategory category, CancellationToken cancellationToken = default);

    void Update(EventCategory category);
}
