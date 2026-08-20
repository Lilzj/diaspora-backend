using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A neighbourhood profile changed.
/// <para>
/// <b>Treat this as "re-read the profile", not as a diff.</b> These are living summaries revised as
/// places change, so the event fires often and carries the changed field names only so a cache can
/// be narrowed - never so a consumer can reconstruct the prose. The authority is
/// <c>INeighborhoodDirectory</c>.
/// </para>
/// <para>
/// <see cref="IsApproved"/> is what a consumer should gate on. An editor draft lands unapproved and
/// a consumer that rendered it would publish unreviewed prose about somebody home under the
/// platform name.
/// </para>
/// </summary>
public sealed record NeighborhoodSummaryUpdatedIntegrationEvent : IntegrationEvent
{
    /// <summary>The Location area. The identifier every consumer already shares.</summary>
    public required Guid AreaId { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public required string CountryCode { get; init; }

    /// <summary>Which parts moved. For cache narrowing, not for reconstruction.</summary>
    public IReadOnlyList<string> ChangedFields { get; init; } = [];

    /// <summary>Whether an approver has signed the current profile off. Gate on this.</summary>
    public required bool IsApproved { get; init; }

    public Guid? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
