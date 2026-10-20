using PaidMoney = Diaspora.Modules.Financial.Contracts.V1.Responses.MoneyResponse;

namespace Diaspora.Modules.Creator.Contracts.V1.Responses;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-013 (recommendations) and the sponsored-content list.
//
// Both mirror the client's `src/types/creator-economy.ts` field for field — `CreatorRecommendation`
// and `SponsoredContentItem` — so connecting them is a change of URL and nothing else.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// One suggestion, already ranked.
/// <para>
/// The composer's own shape. It reaches the wire through <c>ICreatorRecommendationProvider</c> and
/// the Recommendations module's <c>CreatorRecommendationResponse</c>, which has the same fields.
/// </para>
/// <para>
/// <c>recommendationId</c> is stable per underlying item and state (<c>milestone:{id}</c>,
/// <c>challenge:{id}</c>, <c>grant:{id}</c>, <c>course:{id}</c>, <c>profile:{key}</c>,
/// <c>verification:…</c>), which is what lets a dismissal stick.
/// </para>
/// </summary>
public sealed record CreatorRecommendationResponse
{
    public required string RecommendationId { get; init; }

    /// <summary>One of the client's kinds: <c>course</c>, <c>challenge</c>, <c>grant</c>, <c>verification</c>, <c>improvement</c>…</summary>
    public required string Kind { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>Why the platform is suggesting it, from the caller's own state.</summary>
    public required string Reason { get; init; }

    public string? Icon { get; init; }

    public required string ActionLabel { get; init; }

    public required string ActionRoute { get; init; }

    /// <summary>0–1. For emphasis only; the list is already in order.</summary>
    public double? Confidence { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>A delivery figure, in the client's <c>CreatorMetric</c> shape.</summary>
public sealed record SponsoredMetricResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required double Value { get; init; }

    public required string Format { get; init; }

    public required bool HigherIsBetter { get; init; }

    public string? Definition { get; init; }
}

/// <summary>
/// A creator's paid promotion over one of their own content items.
/// <para>
/// A view over B15 — a catalogue placement or an advertising campaign — never a second record of
/// it. <c>placementId</c> is the B15 placement for a catalogue purchase and null for a campaign;
/// <c>campaignId</c> is the reverse, so the client can open the right B15 screen.
/// </para>
/// </summary>
public sealed record SponsoredContentItemResponse
{
    public Guid? PlacementId { get; init; }

    /// <summary>The advertising campaign, when the promotion is one. Not on the client type yet; additive.</summary>
    public Guid? CampaignId { get; init; }

    public required Guid ContentId { get; init; }

    /// <summary>The client's content vocabulary: <c>Guide</c>, <c>Job</c>, <c>Marketplace</c>, <c>Housing</c>, <c>Provider</c>…</summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// What the content is called, from the module that owns it; a campaign's own headline when the
    /// content module has no title to give; and the content type's name when neither exists.
    /// </summary>
    public required string ContentTitle { get; init; }

    public string? ProductCode { get; init; }

    public string? PackageLabel { get; init; }

    public required string State { get; init; }

    public string? RejectionReason { get; init; }

    /// <summary>B15's money shape — <c>amount</c>, <c>currency</c>, <c>minorUnits</c>, <c>exponent</c> — which is the client's <c>Money</c>.</summary>
    public PaidMoney? AmountPaid { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public required bool IsRenewable { get; init; }

    public IReadOnlyList<SponsoredMetricResponse>? Performance { get; init; }
}
