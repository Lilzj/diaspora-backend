using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A publication froze an immutable snapshot of a guide's content.
/// <para>
/// Fires alongside <see cref="GuidePublishedIntegrationEvent"/> — that event says the guide is
/// visible, this one says its history grew. A consumer that only renders cards can ignore this
/// entirely; one that audits editorial history, or diffs what changed between revisions, keys on
/// the (guide, version-number) pair, which is unique and never reused.
/// </para>
/// </summary>
public sealed record GuideVersionCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    public required Guid VersionId { get; init; }

    /// <summary>Monotonic per guide, starting at <c>1</c>. Never reused, even after restores.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>Who published the revision — the author, or a staff editor.</summary>
    public required Guid EditorId { get; init; }

    public string? RevisionNotes { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The member who wrote the guide — B13.1.
    /// <para>
    /// Distinct from <see cref="EditorId"/>, which is whoever published the revision and may be
    /// a staff editor. An editorial-history consumer that conflated the two would attribute
    /// somebody's guide to the last person who touched it.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the guide could not be read.</para>
    /// </summary>
    public Guid? AuthorUserId { get; init; }
}
