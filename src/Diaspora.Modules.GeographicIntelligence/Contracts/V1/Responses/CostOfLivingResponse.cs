namespace Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;

/// <summary>
/// One category of a cost-of-living breakdown.
/// <para>
/// The record exists and the list it lives in is empty on every response this platform can serve
/// today. That is deliberate: the shape is frozen now so that contracting a cost-of-living source
/// later populates a field rather than changing a contract.
/// </para>
/// </summary>
public sealed record CostOfLivingCategoryResponse
{
    /// <summary><c>housing</c>, <c>food</c>, <c>transport</c>, <c>utilities</c>, <c>healthcare</c>, <c>leisure</c>, <c>other</c>.</summary>
    public required string Id { get; init; }

    public required string Label { get; init; }

    /// <summary>Index where 100 is the baseline named on the response.</summary>
    public required int Index { get; init; }

    public decimal? TypicalMonthlyAmount { get; init; }

    public string? TypicalMonthlyCurrency { get; init; }
}

/// <summary>
/// What it costs to live somewhere.
/// <para>
/// <b>Since 17 Sep 2026: rent from Diaspora's own listings, where there are enough.</b> The
/// <c>housing</c> category is the median monthly asking rent of the homes listed in the
/// neighbourhood as an index against the country's median, in the country's most-listed currency,
/// with the sample size in <see cref="Freshness"/>. The other five categories and
/// <see cref="OverallIndex"/> stay empty, because nothing measures them. What follows is the
/// reasoning that still holds for them.
/// </para>
/// <para>
/// <b>One real band, five categories nobody has a source for, and a response that says which is
/// which.</b> B11 records cost of living as one moderated ordinal band per neighbourhood. The
/// client's surface asks for six category indices against a national baseline in a stated
/// currency. Six cannot be derived from one — the frontend register's own words are that "a
/// three-value band cannot be expanded into six category indices without inventing five" — and
/// this response is the backend agreeing with it in a machine-readable way.
/// </para>
/// <para>
/// <b><see cref="OverallIndex"/> is therefore nullable, which is a documented deviation from the
/// client's <c>CostOfLivingIndex.overallIndex: number</c>.</b> The alternative was to publish a
/// number: map five bands onto an index against a baseline nobody measured, in a currency nobody
/// chose. An index of 118 is a claim about how much more expensive somewhere is, to the percent,
/// and this platform does not know that. The band it does know travels in <see cref="Band"/>,
/// which is real, moderated and dated.
/// </para>
/// </summary>
public sealed record CostOfLivingResponse
{
    public required string NeighborhoodId { get; init; }

    public required Guid AreaId { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// True when at least one category index is published — the rent index, where the
    /// neighbourhood and its country have enough homes listed.
    /// <para>
    /// Explicit rather than inferable from nulls, matching the flag B4 already ships on its
    /// neighbourhood stats, so a client renders "not yet available" rather than a zero index.
    /// </para>
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// The real assessed band: <c>VeryLow</c> … <c>VeryHigh</c>, or null where nobody has
    /// assessed it. High means expensive.
    /// </summary>
    public string? Band { get; init; }

    /// <summary>0–100 encoding of <see cref="Band"/>. Null when unassessed. Carries no more precision than the band.</summary>
    public int? BandScore { get; init; }

    /// <summary>
    /// Null until every category has a source. Never derived from the band, and never the rent
    /// index under a broader name.
    /// </summary>
    public int? OverallIndex { get; init; }

    /// <summary>What 100 would mean. Null while <see cref="OverallIndex"/> is.</summary>
    public string? BaselineName { get; init; }

    /// <summary>The currency figures would be quoted in. Null while there are none.</summary>
    public string? Currency { get; init; }

    /// <summary>
    /// The categories that are measured, and only those: <c>housing</c> where the samples allow,
    /// nothing else today. An absent category is not measured, not zero.
    /// </summary>
    public IReadOnlyList<CostOfLivingCategoryResponse> Categories { get; init; } = [];

    public required DataFreshnessResponse Freshness { get; init; }
}
