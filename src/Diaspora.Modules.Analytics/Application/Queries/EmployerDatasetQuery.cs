using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// The caller's own hiring analytics, in the shared Phase 15 dataset shape.
/// <para>
/// FRONTEND-PHASE15-GAP-012, served at <c>GET /api/v1/analytics/employers/me</c>. The register's
/// aggregation, taken literally: job-level rollups summed to the employer.
/// </para>
/// <para>
/// <b>By the member, never the employer profile.</b> Jobs keeps an employer profile per member with
/// its own identifier and names it on every event. <c>JobPublished</c> and
/// <c>ApplicationSubmitted</c> also carry <c>EmployerUserId</c>, and where an application arrives
/// without one it is attributed through the job's publication. A route keyed by the profile would
/// answer nothing for any employer.
/// </para>
/// <para>
/// <b><c>/me</c>, and no <c>/employers/{id}</c>.</b> The seller and landlord routes' reasoning: a
/// route that cannot name another employer cannot leak one. Administrative reads belong on the
/// administration website.
/// </para>
/// <para>
/// <b>What it will not say.</b> Search appearances are not recorded for a job, so they are not in
/// the set. There is no candidate funnel — shortlists, interviews, offers and hires are not counted —
/// because at those stages a count names a person, and there is no breakdown of applicants by
/// anything at all: §19 calls a demographic breakdown both a privacy failure and, in most
/// jurisdictions, an employment-law one. Nothing here identifies an applicant.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.ContentReadOwn)]
public sealed record GetEmployerAnalyticsDatasetQuery : IQuery<AnalyticsDatasetResponse>
{
    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }

    /// <summary>Whether to include the views series. Off by default, as the seller dataset's is.</summary>
    public bool IncludeSeries { get; init; }
}

public sealed class GetEmployerAnalyticsDatasetQueryHandler
    : IQueryHandler<GetEmployerAnalyticsDatasetQuery, AnalyticsDatasetResponse>
{
    private readonly AnalyticsOwnerDatasetComposer _composer;

    public GetEmployerAnalyticsDatasetQueryHandler(AnalyticsOwnerDatasetComposer composer) =>
        _composer = composer;

    public Task<Result<AnalyticsDatasetResponse>> Handle(
        GetEmployerAnalyticsDatasetQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _composer.ComposeAsync(
            AnalyticsOwnerDatasetProfile.Employer,
            query.Period,
            query.From,
            query.To,
            query.Timezone,
            query.IncludeSeries,
            cancellationToken);
    }
}
