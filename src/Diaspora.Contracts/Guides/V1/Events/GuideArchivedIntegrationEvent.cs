using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A guide left circulation without being deleted.
/// <para>
/// An archived guide is out of search and browse but its permalink still resolves for anybody who
/// saved it — the knowledge is dated, not wrong. A search index should drop the document; a
/// bookmark list should keep the entry and mark it archived rather than orphan it.
/// </para>
/// </summary>
public sealed record GuideArchivedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>Null when a sweeper or another module's event archived it rather than a person.</summary>
    public Guid? ArchivedBy { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }

    /// <summary>
    /// The member who wrote the guide — B13.1.
    /// <para>
    /// Carried precisely because <see cref="ArchivedBy"/> so often is not: archival is reachable
    /// from staff and from the moderation projection as well as from the author, and an author
    /// whose advice left circulation without their doing it is the one person who must hear
    /// about it.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the guide could not be read.</para>
    /// </summary>
    public Guid? AuthorUserId { get; init; }
}
