namespace Diaspora.Modules.CommunityLife.Integration;

/// <summary>
/// The read surface other modules use to look events up.
/// <para>
/// A guide that points at the festival it explains, a provider page that lists the workshops that
/// provider runs, a neighbourhood hub that shows what is on this week — each stores an event
/// identifier and asks here, rather than reading this module's aggregates.
/// </para>
/// <para>
/// Only discoverable events resolve, and members-only events resolve for nobody through this
/// contract: a consumer has no way to establish whether its caller belongs to the organizing
/// organization, and answering as though they did would leak a private event to anyone who asked.
/// A consumer that needs the members-only case sends the reader through this module's own endpoint,
/// where the membership check happens.
/// </para>
/// </summary>
public interface IEventDirectory
{
    /// <summary>One event, or null when it does not exist or is not publicly discoverable.</summary>
    Task<EventSummary?> FindAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<EventSummary?> FindBySlugAsync(
        string slug, CancellationToken cancellationToken = default);

    /// <summary>Several at once. The N+1 guard.</summary>
    Task<IReadOnlyDictionary<Guid, EventSummary>> FindManyAsync(
        IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// What is on in a place, soonest first.
    /// <para>
    /// Bounded rather than paged, because every consumer of this is rendering a strip. A consumer
    /// that genuinely wants to page is asking a Community Life question and should use this
    /// module's own endpoint.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<EventSummary>> FindUpcomingAsync(
        string? countryCode = null,
        Guid? neighborhoodAreaId = null,
        Guid? organizationId = null,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
