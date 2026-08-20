using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A guide was soft-deleted.
/// <para>
/// Deletion is soft — the row survives and a restore raises
/// <see cref="GuideRestoredIntegrationEvent"/> — but downstream it must be treated as gone:
/// consumers drop the guide from search indexes, feeds and recommendation pools on this event.
/// Whether the deletion is later reversed is not a consumer's bet to make; a deleted guide shown
/// in results is a leak, an occasionally re-added one is an index refresh.
/// </para>
/// </summary>
public sealed record GuideDeletedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    public required Guid AuthorId { get; init; }

    /// <summary>Null when the author deleted their own guide via the normal route.</summary>
    public Guid? DeletedBy { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}
