using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A published guide's metadata or content changed.
/// <para>
/// Fires only for guides that are already visible — draft edits are the author's business and
/// cross no module boundary. <see cref="ChangedFields"/> is what makes the event usable rather
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
}
