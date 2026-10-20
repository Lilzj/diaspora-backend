using Diaspora.Modules.Analytics.Application.Queries;
using Diaspora.Modules.Analytics.Contracts.V1.Requests;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.SharedKernel.Results;
using MediatR;

namespace Diaspora.Modules.Analytics.Application.Exports;

/// <summary>
/// Reads the dataset a query names, through the route a dashboard would call.
/// <para>
/// <b>Sent, not re-implemented.</b> Each subject's dataset query is dispatched through the mediator,
/// so its own authorisation, ownership check and period rules run exactly as they do for the
/// dashboard. An export therefore cannot carry a figure the caller could not have opened, and there
/// is no second copy of any subject's arithmetic to drift.
/// </para>
/// </summary>
public sealed class AnalyticsSubjectDatasets
{
    private static readonly HashSet<string> RoleSubjects =
        new(StringComparer.Ordinal) { "seller", "landlord", "employer", "provider" };

    private readonly ISender _sender;

    public AnalyticsSubjectDatasets(ISender sender) => _sender = sender;

    /// <summary>
    /// Whether a subject has a dataset this platform can produce for <paramref name="callerId"/>.
    /// <para>
    /// A role subject is served for its own member only — the owner datasets are <c>/me</c> routes —
    /// so a platform reader looking at another seller can view the access answer but not export it.
    /// </para>
    /// </summary>
    public static bool IsExportable(string subjectType, string? subjectId, Guid callerId)
    {
        if (subjectType is "platform" or "creator" or "community")
        {
            return true;
        }

        if (RoleSubjects.Contains(subjectType)
            && (subjectId is null || (Guid.TryParse(subjectId, out var id) && id == callerId)))
        {
            return true;
        }

        return AnalyticsContentTypes.FromRouteSegment(subjectType) != AnalyticsContentType.Unknown
            && Guid.TryParse(subjectId, out _);
    }

    public async Task<Result<AnalyticsDatasetResponse>> ReadAsync(
        AnalyticsQueryPayload query, Guid callerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var type = query.Subject?.Type ?? string.Empty;
        var id = query.Subject?.Id;
        var range = query.Range;

        var period = range?.Preset switch
        {
            "today" => "today",
            "last7d" => "last7d",
            "last30d" => "last30d",
            "last90d" => "last90d",
            "thisYear" => "thisYear",
            _ => "custom"
        };

        var from = period == "custom" ? range?.From : null;
        var to = period == "custom" ? range?.To : null;
        var timezone = range?.Timezone;

        var isSelf = id is null || (Guid.TryParse(id, out var parsed) && parsed == callerId);

        IRequest<Result<AnalyticsDatasetResponse>>? request = type switch
        {
            "platform" => new GetPlatformAnalyticsDatasetQuery
            {
                Period = period, From = from, To = to, Timezone = timezone
            },
            "creator" => new GetCreatorAnalyticsDatasetQuery
            {
                Period = period, From = from, To = to, Timezone = timezone, IncludeSeries = true
            },
            "seller" when isSelf => new GetSellerAnalyticsDatasetQuery
            {
                Period = period, From = from, To = to, Timezone = timezone, IncludeSeries = true
            },
            "landlord" when isSelf => new GetLandlordAnalyticsDatasetQuery
            {
                Period = period, From = from, To = to, Timezone = timezone, IncludeSeries = true
            },
            "employer" when isSelf => new GetEmployerAnalyticsDatasetQuery
            {
                Period = period, From = from, To = to, Timezone = timezone, IncludeSeries = true
            },
            "provider" when isSelf => new GetProviderAnalyticsDatasetQuery
            {
                Period = period, From = from, To = to, Timezone = timezone, IncludeSeries = true
            },
            "community" when !string.IsNullOrWhiteSpace(id) => new GetCommunityAnalyticsDatasetQuery
            {
                CommunityId = id, Period = period, From = from, To = to, Timezone = timezone
            },
            _ when AnalyticsContentTypes.FromRouteSegment(type) != AnalyticsContentType.Unknown
                && !string.IsNullOrWhiteSpace(id) => new GetContentAnalyticsQuery
            {
                ContentType = type, ContentId = id, Period = period, From = from, To = to, Timezone = timezone
            },
            _ => null
        };

        if (request is null)
        {
            // A role subject naming somebody else, or a subject with no dataset at all. Either way
            // there is nothing here this caller can have.
            return RoleSubjects.Contains(type) || type == "community"
                ? Result.Failure<AnalyticsDatasetResponse>(AnalyticsErrors.SubjectNotFound)
                : Result.Failure<AnalyticsDatasetResponse>(AnalyticsErrors.SubjectTypeUnsupported);
        }

        return await _sender.Send(request, cancellationToken).ConfigureAwait(false);
    }
}
