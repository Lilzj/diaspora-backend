using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// One journey, counted step by step from the raw analytics store — FRONTEND-PHASE15-GAP-016.
/// <para>
/// <b>Subjects.</b> One item (<c>job</c>, <c>listing</c>, <c>housing</c>, <c>provider</c> with the
/// item's id) for its owner; the caller's own items in a role (<c>employer</c>, <c>seller</c>,
/// <c>landlord</c>, <c>provider</c> with the caller's own user id, or no id); or the whole platform,
/// which any signed-in member may read for the same reason the platform overview is open to them —
/// it is an aggregate count that names no item and no person.
/// </para>
/// <para>
/// <b>Never invented.</b> The steps are the ones <see cref="AnalyticsFunnelCatalog"/> knows to be
/// recorded; every other step the client names comes back in <c>unavailableSteps</c> with a reason.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record GetAnalyticsFunnelQuery : IQuery<AnalyticsFunnelResponse>
{
    public required string FunnelKey { get; init; }

    public string? SubjectType { get; init; }

    public string? SubjectId { get; init; }

    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }
}

public sealed class GetAnalyticsFunnelQueryHandler
    : IQueryHandler<GetAnalyticsFunnelQuery, AnalyticsFunnelResponse>
{
    private readonly AnalyticsQueryContext _context;
    private readonly IAnalyticsEventRepository _events;
    private readonly AnalyticsOptions _options;

    public GetAnalyticsFunnelQueryHandler(
        AnalyticsQueryContext context,
        IAnalyticsEventRepository events,
        IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _context = context;
        _events = events;
        _options = options.Value;
    }

    public async Task<Result<AnalyticsFunnelResponse>> Handle(
        GetAnalyticsFunnelQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsFunnelResponse>(caller.Error);
        }

        var funnel = AnalyticsFunnelCatalog.Find(query.FunnelKey);

        if (funnel is null)
        {
            return Result.Failure<AnalyticsFunnelResponse>(AnalyticsErrors.FunnelUnknown(query.FunnelKey));
        }

        var resolved = _context.ResolvePeriod(
            query.Period ?? (query.From is null ? null : "custom"),
            query.From,
            query.To,
            query.Timezone);

        if (resolved.IsFailure)
        {
            return Result.Failure<AnalyticsFunnelResponse>(resolved.Error);
        }

        var period = resolved.Value;

        var scoped = await ResolveScopeAsync(funnel, query, caller.Value, cancellationToken)
            .ConfigureAwait(false);

        if (scoped.IsFailure)
        {
            return Result.Failure<AnalyticsFunnelResponse>(scoped.Error);
        }

        var (scope, subject) = scoped.Value;

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        var steps = new List<AnalyticsFunnelStepResponse>(funnel.Steps.Count);

        foreach (var step in funnel.Steps)
        {
            var required = step.Requires
                .Select(key => funnel.Steps.First(candidate => candidate.Key == key).EventType)
                .ToList();

            var count = scope is null
                ? 0
                : await _events
                    .CountFunnelJourneysAsync(scope, step.EventType, required, period.Start, period.End, cancellationToken)
                    .ConfigureAwait(false);

            counts[step.Key] = count;
        }

        var first = funnel.Steps.Count == 0 ? null : funnel.Steps[0];

        for (var index = 0; index < funnel.Steps.Count; index++)
        {
            var step = funnel.Steps[index];
            var count = counts[step.Key];
            var previous = index == 0 ? null : funnel.Steps[index - 1];

            // A ratio between two steps is published only where one is a subset of the other. The
            // later steps are each "of the people who opened it", not "of the people who saved it",
            // so a rate between two siblings would compare unrelated populations.
            double? fromPrevious = previous is not null
                && step.Requires.Contains(previous.Key, StringComparer.Ordinal)
                && counts[previous.Key] > 0
                    ? count / (double)counts[previous.Key]
                    : null;

            double? fromStart = first is not null
                && (index == 0 || step.Requires.Contains(first.Key, StringComparer.Ordinal))
                && counts[first.Key] > 0
                    ? count / (double)counts[first.Key]
                    : null;

            steps.Add(new AnalyticsFunnelStepResponse
            {
                Key = step.Key,
                Label = step.Label,
                Count = count,
                ConversionFromPrevious = fromPrevious,
                ConversionFromStart = fromStart,
                DropOffFromPrevious = fromPrevious is null ? null : 1 - fromPrevious
            });
        }

        var now = _context.Calendar.UtcNow;
        var limitations = new List<string>
        {
            "Each step after the first counts people who opened the item and then took that step; later steps are not nested in one another.",
            "A person is followed on one item at a time, and only where the activity carries a signed-in member.",
            "Opening an item is reported by the app, so an app that did not report an opening leaves that person out of the journey."
        };

        if (period.Start < now.AddDays(-_options.RawEventRetentionDays))
        {
            limitations.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Journeys are kept for {_options.RawEventRetentionDays} days, so the start of this period is not counted."));
        }

        return Result.Success(new AnalyticsFunnelResponse
        {
            Key = funnel.Key,
            Label = funnel.Label,
            Subject = subject,
            Steps = steps,
            UnavailableSteps =
            [
                .. funnel.Unavailable.Select(step => new AnalyticsFunnelUnavailableStepResponse
                {
                    Key = step.Key,
                    Label = step.Label,
                    Reason = step.Reason
                })
            ],
            Range = new AnalyticsTimeRangeResponse
            {
                Preset = AnalyticsQueryContext.ToDatasetPreset(period.Preset),
                From = period.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                To = period.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Timezone = period.TimeZoneId,
                IsDeviceTimezone = false
            },
            Source = "backend",
            Computation = "server-aggregate",
            Freshness = new DataFreshnessResponse
            {
                ComputedAt = now,
                SourceName = "Diaspora analytics",
                Methodology = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Counted at request time from recorded activity: one journey is one person on one item, inside the period. Days are cut in {period.TimeZoneId}."),
                Limitations = limitations
            }
        });
    }

    /// <summary>
    /// Which rows the funnel reads, and what the subject is called on the wire.
    /// <para>
    /// A null scope is a journey with no recorded steps at all — the placement funnel — which is
    /// answered with its unavailable steps rather than refused.
    /// </para>
    /// </summary>
    private async Task<Result<(AnalyticsFunnelScope? Scope, AnalyticsSubjectResponse Subject)>> ResolveScopeAsync(
        AnalyticsFunnelDefinition funnel,
        GetAnalyticsFunnelQuery query,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        var subjectType = query.SubjectType?.Trim().ToLowerInvariant() is { Length: > 0 } type
            ? type
            : "platform";
        var subjectId = string.IsNullOrWhiteSpace(query.SubjectId) ? null : query.SubjectId.Trim();

        var subject = new AnalyticsSubjectResponse { Type = subjectType, Id = subjectId, Label = null };

        if (funnel.ContentType == AnalyticsContentType.Unknown)
        {
            return Result.Success<(AnalyticsFunnelScope?, AnalyticsSubjectResponse)>((null, subject));
        }

        if (subjectType == "platform")
        {
            return Result.Success<(AnalyticsFunnelScope?, AnalyticsSubjectResponse)>(
                (new AnalyticsFunnelScope(funnel.ContentType, null, null), subject with { Id = null }));
        }

        // A role subject is the caller's own items, asked with their own user id or with none.
        if (string.Equals(subjectType, funnel.OwnerSubjectType, StringComparison.Ordinal))
        {
            Guid? owner = subjectId is null
                ? callerId
                : Guid.TryParse(subjectId, out var parsed) ? parsed : null;

            if (owner == callerId || (owner is not null && subjectType != "provider" && _context.IsPlatformReader))
            {
                return Result.Success<(AnalyticsFunnelScope?, AnalyticsSubjectResponse)>(
                    (new AnalyticsFunnelScope(funnel.ContentType, null, owner),
                     subject with { Id = owner!.Value.ToString() }));
            }

            // `provider` is both a role and a content type; any other id is one provider's profile.
            if (subjectType != "provider")
            {
                return Result.Failure<(AnalyticsFunnelScope?, AnalyticsSubjectResponse)>(AnalyticsErrors.SubjectNotFound);
            }
        }

        var contentType = AnalyticsContentTypes.FromRouteSegment(subjectType);

        if (contentType == AnalyticsContentType.Unknown && !AnalyticsQueryPayloads.SubjectTypes.Contains(subjectType))
        {
            return Result.Failure<(AnalyticsFunnelScope?, AnalyticsSubjectResponse)>(AnalyticsErrors.SubjectTypeUnsupported);
        }

        if (contentType != funnel.ContentType)
        {
            return Result.Failure<(AnalyticsFunnelScope?, AnalyticsSubjectResponse)>(AnalyticsErrors.FunnelSubjectMismatch);
        }

        if (!Guid.TryParse(subjectId, out var contentId))
        {
            return Result.Failure<(AnalyticsFunnelScope?, AnalyticsSubjectResponse)>(AnalyticsErrors.SubjectNotFound);
        }

        var ownership = await _context
            .RequireContentOwnerAsync(contentType, contentId, cancellationToken)
            .ConfigureAwait(false);

        if (ownership.IsFailure)
        {
            return Result.Failure<(AnalyticsFunnelScope?, AnalyticsSubjectResponse)>(ownership.Error);
        }

        return Result.Success<(AnalyticsFunnelScope?, AnalyticsSubjectResponse)>(
            (new AnalyticsFunnelScope(contentType, contentId, null),
             subject with { Type = AnalyticsContentTypes.ToSubjectType(contentType), Id = contentId.ToString() }));
    }
}
