using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// The caller's own service-provider analytics, in the shared Phase 15 dataset shape.
/// <para>
/// FRONTEND-PHASE15-GAP-010, served at <c>GET /api/v1/analytics/providers/me</c>: the caller's
/// provider profiles, summed. Until 13 Sep 2026 the provider dashboard ran on the development adapter
/// in every build, was opened by a provider id nothing in the app supplied, and was linked from
/// nowhere.
/// </para>
/// <para>
/// <b>By the member.</b> Unlike a seller or an employer, a provider has no profile identifier standing
/// in for its owner: <c>ProviderPublished</c> names the provider's earliest owner by user id, which is
/// what the provider's content dimension records and what every view, enquiry and save is attributed
/// through. A provider with co-owners reports to the owner it had when it was first published.
/// </para>
/// <para>
/// <b><c>/me</c>, and no <c>/providers/{id}</c>.</b> The register asked for a route by provider id.
/// A route that cannot name another provider cannot leak one, and one provider's figures are already
/// served by the per-item route, which checks ownership of that provider.
/// </para>
/// <para>
/// <b>Enquiries, counted from the first message.</b> A member tapping "Message" opens a
/// <c>ProviderInquiry</c> conversation; it becomes an enquiry when somebody writes in it.
/// </para>
/// <para>
/// <b>What it will not say.</b> No leads — a lead needs a reply joined to the enquiry, which nothing
/// defines — and no funnel. Call and directions taps are recorded nowhere. Search appearances are
/// stored but reach is not published. Nothing here names a member who viewed, wrote to or saved a
/// provider.
/// </para>
/// </summary>
[RequirePermission(AnalyticsPermissions.ContentReadOwn)]
public sealed record GetProviderAnalyticsDatasetQuery : IQuery<AnalyticsDatasetResponse>
{
    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }

    /// <summary>Whether to include the views series. Off by default, as the seller dataset's is.</summary>
    public bool IncludeSeries { get; init; }
}

public sealed class GetProviderAnalyticsDatasetQueryHandler
    : IQueryHandler<GetProviderAnalyticsDatasetQuery, AnalyticsDatasetResponse>
{
    private readonly AnalyticsOwnerDatasetComposer _composer;

    public GetProviderAnalyticsDatasetQueryHandler(AnalyticsOwnerDatasetComposer composer) =>
        _composer = composer;

    public Task<Result<AnalyticsDatasetResponse>> Handle(
        GetProviderAnalyticsDatasetQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _composer.ComposeAsync(
            AnalyticsOwnerDatasetProfile.Provider,
            query.Period,
            query.From,
            query.To,
            query.Timezone,
            query.IncludeSeries,
            cancellationToken);
    }
}
