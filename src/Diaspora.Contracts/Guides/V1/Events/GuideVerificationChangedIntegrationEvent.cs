using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// Trust's opinion of a guide changed, in any direction.
/// <para>
/// <b>This is the "guide verified" fact</b> — and also the pending, rejected and revoked facts,
/// because one event covers the whole verification lifecycle. Every consumer holds the same
/// single piece of state, the current <see cref="State"/>, and four event types would hand each
/// of them a four-way merge that this record performs once at the source. A consumer renders a
/// badge when the state is <c>Verified</c> and removes it otherwise; the transition that led
/// here is Trust's business, not the subscriber's.
/// </para>
/// <para>
/// A revocation a consumer misses leaves a verified badge on withdrawn advice — worse than never
/// having shown one. Delivery is at-least-once and unordered, so consumers deduplicate on
/// <c>EventId</c> and guard ordering on <see cref="ChangedAt"/>.
/// </para>
/// </summary>
public sealed record GuideVerificationChangedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>The <c>GuideVerificationState</c> name — <c>None</c>, <c>Pending</c>, <c>Verified</c>, <c>Revoked</c>.</summary>
    public required string State { get; init; }

    /// <summary>Trust's verification identifier. Null when the state is <c>None</c>.</summary>
    public Guid? VerificationId { get; init; }

    /// <summary>The badge granted. Null unless the state is <c>Verified</c>.</summary>
    public string? Badge { get; init; }

    /// <summary>Why the state moved. Null unless Trust supplied one — typically on rejection or revocation.</summary>
    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// The member who wrote the guide — B13.1.
    /// <para>
    /// <b>Trust decided this, not the author.</b> A revocation says the platform has stopped
    /// vouching for advice somebody wrote in good faith, and <see cref="Reason"/> is written for
    /// them. Until B13.1 the contract could describe the decision and could not address the one
    /// person it was about.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the guide could not be read.</para>
    /// </summary>
    public Guid? AuthorUserId { get; init; }
}
