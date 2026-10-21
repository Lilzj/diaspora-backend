using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A guide's metadata or content changed.
/// <para>
/// <b>Fires for drafts too</b> — this summary used to say it did not, and a consumer that believed it
/// told strangers about drafts (21 Oct 2026). Whether anybody but the author may read the guide is
/// <see cref="IsReadableByOthers"/>; a consumer that tells anybody else reads it first.
/// <see cref="ChangedFields"/> is what makes the event usable rather
/// than merely present: a consumer that reindexes on every edit reindexes on a media caption fix;
/// one that reads the field list reindexes only when something searchable moved. The names are
/// the response DTO's field names, so a consumer never has to know B8's internal property names.
/// </para>
/// </summary>
public sealed record GuideUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>Null when a projection updated it from another module's event.</summary>
    public Guid? ChangedBy { get; init; }

    /// <summary>Wire field names that moved: <c>title</c>, <c>summary</c>, <c>sections</c>, <c>tags</c>.</summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// The member who wrote the guide — B13.1.
    /// <para>
    /// Distinct from <see cref="ChangedBy"/>, and the distinction is the reason this exists: a
    /// guide with approved contributors can be edited by somebody who did not write it, and an
    /// author whose work was changed by another hand is the one who has to be told.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the guide could not be read.</para>
    /// </summary>
    public Guid? AuthorUserId { get; init; }

    /// <summary>
    /// Whether anybody but the author may open the guide after this change — published, Public or
    /// Unlisted, not hidden by moderation, not deleted — 21 Oct 2026. Appended, never inserted.
    /// <para>
    /// <b>What decides whether a consumer may tell people who are not the author.</b> A draft edit is
    /// the author's business; a guide made private or hidden is nobody else's any more. Defaults to
    /// false, so a message written before the field existed fails closed.
    /// </para>
    /// </summary>
    public bool IsReadableByOthers { get; init; }
}
