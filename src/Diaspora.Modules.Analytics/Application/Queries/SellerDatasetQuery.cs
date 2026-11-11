using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// The caller's own marketplace analytics, in the shared Phase 15 dataset shape.
/// <para>
/// FRONTEND-PHASE15-GAP-011, served at <c>GET /api/v1/analytics/sellers/me</c>. The register's
/// aggregation, taken literally: listing-level rollups summed to the seller.
/// </para>
/// <para>
/// <b>An <c>AnalyticsDataset</c>, deliberately, and not a record of its own.</b> Five analytics
/// routes on this platform answer with bespoke records — organisation, event, country,
/// notification, recommendation — and every one of them is served and still unusable by the
/// Phase 15 dashboards, because those render datasets. A seller rollup shaped
/// <c>{ viewCount, totalSales }</c> would have been the sixth.
/// </para>
/// <para>
/// <b><c>/me</c>, and no <c>/sellers/{id}</c> at any authorization level.</b> The creator route's
/// reasoning, which is the strongest guarantee available: a route that cannot name another seller
/// cannot leak one. The register also asked for platform administrators; administrative reads
/// belong on the administration website, which will call a route of its own.
/// </para>
/// <para>
/// <b>Enquiries are in the set</b>, counted when a buyer first writes in a conversation about a
/// listing; opening one and writing nothing is not an enquiry.
/// </para>
/// <para>
/// <b>What it will not say.</b> Impressions are not recorded for a listing, and offers are not
/// counted. Revenue is in the set and served as not measured — the wallet is where a seller's money
/// is, and the financial platform is the only authority on it. Nothing here names a buyer: a buyer is
/// a person, and a sales or enquiry count is analytics where a buyer list is not.
/// </para>
/// <para>
/// Composed by <see cref="AnalyticsOwnerDatasetComposer"/>, which the landlord, employer and provider
/// datasets share: they are one question asked of four content types.
/// </para>
/// </summary>
[RequirePermission(AnalyticsPermissions.ContentReadOwn)]
public sealed record GetSellerAnalyticsDatasetQuery : IQuery<AnalyticsDatasetResponse>
{
    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }

    /// <summary>Whether to include the views series. Off by default, as the creator dataset's is.</summary>
    public bool IncludeSeries { get; init; }
}

public sealed class GetSellerAnalyticsDatasetQueryHandler
    : IQueryHandler<GetSellerAnalyticsDatasetQuery, AnalyticsDatasetResponse>
{
    private readonly AnalyticsOwnerDatasetComposer _composer;

    public GetSellerAnalyticsDatasetQueryHandler(AnalyticsOwnerDatasetComposer composer) =>
        _composer = composer;

    public Task<Result<AnalyticsDatasetResponse>> Handle(
        GetSellerAnalyticsDatasetQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _composer.ComposeAsync(
            AnalyticsOwnerDatasetProfile.Seller,
            query.Period,
            query.From,
            query.To,
            query.Timezone,
            query.IncludeSeries,
            cancellationToken);
    }
}
