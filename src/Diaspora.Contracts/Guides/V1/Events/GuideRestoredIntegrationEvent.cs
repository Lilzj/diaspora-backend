using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A guide re-entered circulation.
/// <para>
/// One event for both ways out of limbo — restore from archive and restore from soft deletion —
/// because every consumer reacts the same way: put the guide back. <see cref="RestoredFrom"/>
/// records which it was, for the rare consumer that treated the two exits differently. The
/// payload deliberately carries no content: a consumer rebuilding an index entry re-reads through
/// the Guides directory, because the guide may have been edited since the index last saw it.
/// </para>
/// </summary>
public sealed record GuideRestoredIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary><c>Archived</c> or <c>Deleted</c> — the state the guide came back from.</summary>
    public required string RestoredFrom { get; init; }

    /// <summary>Null when a sweeper or another module's event restored it rather than a person.</summary>
    public Guid? RestoredBy { get; init; }

    public required DateTimeOffset RestoredAt { get; init; }

    /// <summary>
    /// The member who wrote the guide — B13.1. The counterpart to the archival contract's:
    /// somebody whose guide came back deserves to hear that as much as they deserved to hear it
    /// had gone.
    /// <para>Null on events published before B13.1, and null when the guide could not be read.</para>
    /// </summary>
    public Guid? AuthorUserId { get; init; }
}
