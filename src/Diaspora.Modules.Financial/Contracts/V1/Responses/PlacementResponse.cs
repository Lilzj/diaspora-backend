namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// A purchased placement over a subject.
/// <para>
/// <b>Deliberately not called <c>isFeatured</c>.</b> The client already has that field on marketplace
/// listings, housing listings, events and organizations, where it means a moderator's editorial
/// decision — free, and nothing to do with money. A placement is what somebody <em>bought</em>, and
/// giving the two one name would make "did this rank because it is good or because it was paid for"
/// unanswerable.
/// </para>
/// <para>
/// <b><see cref="EndsAt"/> is on the response so a rail can expire an entry without a second round
/// trip.</b> Marketplace's own featuring contract has carried the same field since B10 for the same
/// reason.
/// </para>
/// </summary>
public sealed record PlacementResponse
{
    public required Guid PlacementId { get; init; }

    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    /// <summary>
    /// <c>Featured</c>, <c>Bump</c>, <c>Spotlight</c>, <c>CategoryHighlight</c>, <c>Boost</c> or
    /// <c>Premium</c>. The first four are Marketplace's own vocabulary, unchanged.
    /// </summary>
    public required string Kind { get; init; }

    public required string Status { get; init; }

    /// <summary>Whether it is in force right now.</summary>
    public required bool IsActive { get; init; }

    /// <summary>What a ranking consumer should weight the subject by. 1.0 is no change.</summary>
    public required decimal PriorityWeight { get; init; }

    public required string ProductCode { get; init; }

    public string? CountryCode { get; init; }

    public string? PolicyVersion { get; init; }

    /// <summary>
    /// Whether the buyer has asked for it to renew itself.
    /// <para>
    /// A stored preference rather than a subscription: renewal still buys the product again at
    /// whatever it costs then. What it removes is the member having to be holding their phone at the
    /// right moment.
    /// </para>
    /// </summary>
    public required bool AutoRenewEnabled { get; init; }

    /// <summary>When the buyer paused it. Null unless paused; the clock is stopped while it is set.</summary>
    public DateTimeOffset? PausedAt { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    /// <summary>When it lapses. Null on an open-ended placement.</summary>
    public DateTimeOffset? EndsAt { get; init; }
}
