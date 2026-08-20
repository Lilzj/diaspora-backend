using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A member reported a listing.
/// <para>
/// <b>The one contract in this module whose consumers are all internal to the platform's safety
/// surface.</b> It exists so a central moderation console, a Trust fraud signal and an alerting
/// rule can each see marketplace reports without polling this module.
/// </para>
/// <para>
/// <b>It carries no reporter identity.</b> Who reported a listing is the most retaliation-prone
/// fact this module holds, and a seller with access to any consumer of this bus must not be able
/// to learn it. <see cref="TotalReports"/> is what a triage rule actually needs.
/// </para>
/// </summary>
public sealed record ListingReportedIntegrationEvent : IntegrationEvent
{
    public required Guid ReportId { get; init; }

    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    /// <summary>
    /// <c>Fraud</c>, <c>Duplicate</c>, <c>Spam</c>, <c>Prohibited</c>, <c>Counterfeit</c>,
    /// <c>Miscategorised</c>, <c>Offensive</c>, <c>Other</c>.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>How many distinct members have now reported this listing.</summary>
    public required int TotalReports { get; init; }

    /// <summary>Whether the report moved the listing into the moderation queue.</summary>
    public required bool WasFlagged { get; init; }

    public required DateTimeOffset ReportedAt { get; init; }
}
