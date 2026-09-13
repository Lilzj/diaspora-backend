using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// The caller's own housing analytics, in the shared Phase 15 dataset shape.
/// <para>
/// FRONTEND-PHASE15-GAP-013, served at <c>GET /api/v1/analytics/landlords/me</c>. The register's
/// aggregation, taken literally: listing-level rollups summed to the landlord.
/// </para>
/// <para>
/// <b>By the member, never the landlord profile.</b> Housing keeps a landlord profile per member
/// with its own identifier, and every housing event names that profile. Analytics keys a home's rows
/// to the member who owns it — <c>HousingPublished</c> carries <c>LandlordUserId</c> for exactly
/// that, and a viewing, which names only the profile, is attributed through the listing's
/// publication. A route keyed by profile id would answer nothing for every landlord, which is what
/// the seller dashboard did until 13 Sep 2026.
/// </para>
/// <para>
/// <b><c>/me</c>, and no <c>/landlords/{id}</c>.</b> The seller route's reasoning: a route that
/// cannot name another landlord cannot leak one. Administrative reads belong on the administration
/// website.
/// </para>
/// <para>
/// <b>What it will not say.</b> Impressions, saves, enquiries and applications are not recorded for
/// a home, so they are not in the set. There is no revenue tile: rent is not taken through Diaspora.
/// Nothing here describes a prospective tenant — not who booked a viewing, and not where they come
/// from. §20 is explicit that housing is where a breakdown of enquirers by country or community
/// would do the most harm, and this dataset has no dimension to break down by.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.ContentReadOwn)]
public sealed record GetLandlordAnalyticsDatasetQuery : IQuery<AnalyticsDatasetResponse>
{
    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }

    /// <summary>Whether to include the views series. Off by default, as the seller dataset's is.</summary>
    public bool IncludeSeries { get; init; }
}

public sealed class GetLandlordAnalyticsDatasetQueryHandler
    : IQueryHandler<GetLandlordAnalyticsDatasetQuery, AnalyticsDatasetResponse>
{
    private readonly AnalyticsOwnerDatasetComposer _composer;

    public GetLandlordAnalyticsDatasetQueryHandler(AnalyticsOwnerDatasetComposer composer) =>
        _composer = composer;

    public Task<Result<AnalyticsDatasetResponse>> Handle(
        GetLandlordAnalyticsDatasetQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _composer.ComposeAsync(
            AnalyticsOwnerDatasetProfile.Landlord,
            query.Period,
            query.From,
            query.To,
            query.Timezone,
            query.IncludeSeries,
            cancellationToken);
    }
}
