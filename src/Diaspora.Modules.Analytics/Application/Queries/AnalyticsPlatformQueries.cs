using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Queries;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE15-GAP-025 (access) and -030 (definitions).
//
// The register calls the first the linchpin — "every other gap's authorization line depends on it
// being real" — and it is deliberately the smallest handler in the module: it answers what the
// caller may do, from the caller's permissions and the subject's ownership, and nothing else.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// What the caller may see for one subject.
/// <para>
/// <b>Answers rather than refuses.</b> A caller with no access receives an all-false set and a 200,
/// so a screen renders "not available to you" instead of an error — and so the existence of an
/// administrative dashboard is not confirmed by a status code. The only 401 is an unauthenticated
/// caller, which is a fact about the request rather than about the subject.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record GetAnalyticsAccessQuery : IQuery<AnalyticsAccessResponse>
{
    /// <summary><c>creator</c>, or one of the content subject types.</summary>
    public string? SubjectType { get; init; }

    public string? SubjectId { get; init; }
}

public sealed class GetAnalyticsAccessQueryHandler
    : IQueryHandler<GetAnalyticsAccessQuery, AnalyticsAccessResponse>
{

    /// <summary>
    /// Subjects that name a <b>role the caller holds</b> rather than a piece of content.
    /// </summary>
    /// <remarks>
    /// Content subjects are resolved by ownership of the row. These are resolved by identity:
    /// the subject id is the member's own user id, so "may I see this" and "is this me" are the
    /// same question. A platform reader may see any of them.
    /// <para>
    /// <b><c>provider</c> is both.</b> The provider dashboard is a role subject asked with the caller's
    /// own id, and <c>provider</c> is also a content type, asked with one provider's id. The two
    /// cannot be confused — an identity check passes only for the caller's own user id, which is no
    /// provider's id — so a provider subject that is not the caller falls through to the ownership
    /// check below, as it did before the dashboard existed. Until 13 Sep 2026 only that check ran, and
    /// the dashboard would have been refused to every provider it was opened for.
    /// </para>
    /// </remarks>
    private static readonly HashSet<string> RoleSubjects =
        new(StringComparer.Ordinal) { "seller", "landlord", "employer", "provider" };

    private readonly AnalyticsQueryContext _context;

    public GetAnalyticsAccessQueryHandler(AnalyticsQueryContext context) => _context = context;

    public async Task<Result<AnalyticsAccessResponse>> Handle(
        GetAnalyticsAccessQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsAccessResponse>(caller.Error);
        }

        var subjectType = query.SubjectType?.Trim().ToLowerInvariant() ?? "creator";
        var isPlatformReader = _context.IsPlatformReader;

        // ── Role subjects could never be viewed by anybody ────────────────────
        //
        // This was `canView = subjectType == "creator"`, then a fallback through
        // `AnalyticsContentTypes.FromRouteSegment` — which maps **content** types
        // only: post, guide, listing, job, housing, marketplace, event,
        // organization. `seller`, `landlord` and `employer` are **role**
        // subjects, so the lookup returned `Unknown`, the block was skipped, and
        // `canView` stayed false for every possible input.
        //
        // There was no request that made `/analytics/seller`,
        // `/analytics/landlord` or the employer dashboard return true. All three
        // are linked from Profile → Analytics and rendered "Not available to
        // you" to the only person entitled to them: a member with a seller
        // profile and three published listings was refused their own selling
        // figures (UAT-125).
        //
        // A role subject is the caller's own standing in that role, and the
        // client sends the caller's user id as its identifier — the same
        // protection the creator subject gets from carrying no id at all. So the
        // rule is ownership: you may read a role subject when it is you.
        // The platform overview is aggregate counts any signed-in member may read, matching
        // GET /analytics/platform (20 Oct 2026); the administrative views stay staff-only.
        var canView = subjectType is "creator" or "platform";

        if (!canView && RoleSubjects.Contains(subjectType))
        {
            canView = isPlatformReader
                || (Guid.TryParse(query.SubjectId, out var roleSubjectId)
                    && roleSubjectId == caller.Value);
        }

        if (!canView && Guid.TryParse(query.SubjectId, out var contentId))
        {
            var contentType = AnalyticsContentTypes.FromRouteSegment(subjectType);

            if (contentType != AnalyticsContentType.Unknown)
            {
                var owner = await _context
                    .RequireContentOwnerAsync(contentType, contentId, cancellationToken)
                    .ConfigureAwait(false);

                canView = owner.IsSuccess;
            }
        }

        return Result.Success(new AnalyticsAccessResponse
        {
            Subject = new AnalyticsSubjectResponse
            {
                Type = subjectType,
                Id = subjectType == "creator" ? null : query.SubjectId,
                Label = null
            },
            CanView = canView,

            // Money is B15's and CREATOR-GAP-008's. Reporting this as true would tell a client to
            // ask for a revenue figure this platform has no authority to produce.
            CanViewFinancial = false,
            CanViewAdministrative = isPlatformReader,

            // Comparison across subjects is FRONTEND-PHASE15-GAP-006's contract and is not built.
            CanCompare = false,

            // Export is FRONTEND-PHASE15-GAP-026 and report building is -027. Both are named in the
            // register as later phases; reporting them as available would render an action that 404s.
            CanExport = false,
            CanBuildReports = false,
            SupportedRanges = SupportedRanges(),
            SupportedComparisons = ["previousPeriod"],
            RestrictedMetrics = RestrictedMetrics(canView),
            ReportingTimezone = _context.Calendar.TimeZoneId
        });
    }

    /// <summary>
    /// Which of the client's eleven presets this deployment answers.
    /// <para>
    /// Six, which is the Phase 7 set. The five the Phase 15 model also carries —
    /// <c>yesterday</c>, <c>previousMonth</c>, <c>previousYear</c>, <c>allTime</c>,
    /// <c>thisMonth</c> — are absent because nothing serves them, and §6 makes which ranges a
    /// subject offers a capability precisely so a client need not guess.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> SupportedRanges() =>
        ["today", "last7d", "last30d", "last90d", "thisYear", "custom"];

    /// <summary>
    /// What is withheld from this caller.
    /// <para>
    /// Distinct from what this deployment does not measure: one is about the caller and travels
    /// here, the other is about the platform and travels on the dataset. Conflating them would tell
    /// a creator that reach is being kept from them.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> RestrictedMetrics(bool canView) =>
        canView ? [] : AnalyticsMetricCatalog.CreatorHeadline;
}

/// <summary>
/// The metric catalogue.
/// <para>
/// FRONTEND-PHASE15-GAP-030. Authenticated rather than public: the definitions describe what this
/// deployment measures and how, which is operational detail rather than a member-facing secret, but
/// it is also a map of the analytics surface and there is no reason for it to be anonymous.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record GetAnalyticsDefinitionsQuery : IQuery<AnalyticsDefinitionsResponse>;

public sealed class GetAnalyticsDefinitionsQueryHandler
    : IQueryHandler<GetAnalyticsDefinitionsQuery, AnalyticsDefinitionsResponse>
{
    private readonly AnalyticsOptions _options;

    public GetAnalyticsDefinitionsQueryHandler(IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public Task<Result<AnalyticsDefinitionsResponse>> Handle(
        GetAnalyticsDefinitionsQuery query, CancellationToken cancellationToken)
    {
        var freshness = string.Create(
            CultureInfo.InvariantCulture,
            $"Recomputed about every {_options.ProjectionIntervalSeconds} seconds from recorded activity. Raw activity is kept for {_options.RawEventRetentionDays} days and daily figures for {_options.RollupRetentionDays}.");

        var definitions = AnalyticsMetricCatalog.Definitions
            .Select(definition => new AnalyticsDefinitionResponse
            {
                Key = definition.Key,
                CatalogueKey = definition.CatalogueKey,
                Label = definition.Label,
                Definition = definition.Definition,
                Formula = definition.Formula,
                Format = AnalyticsMetricAssembler.FormatName(definition.Format),
                HigherIsBetter = definition.HigherIsBetter,
                PrivacyClass = AnalyticsMetricAssembler.PrivacyName(definition.Privacy),
                Owner = definition.Owner,
                FreshnessPolicy = freshness,
                TrustClass = definition.Trust switch
                {
                    AnalyticsTrustClass.Server => "server",
                    AnalyticsTrustClass.Client => "client",
                    _ => "derived"
                },
                Status = definition.Availability switch
                {
                    AnalyticsMetricAvailability.Available => "CONNECTED",
                    AnalyticsMetricAvailability.AwaitingSource => "AWAITING_SOURCE",
                    _ => "NOT_MEASURED"
                }
            })
            .ToList();

        return Task.FromResult(Result.Success(new AnalyticsDefinitionsResponse
        {
            Version = AnalyticsMetricCatalog.Version,
            Definitions = definitions
        }));
    }
}

/// <summary>
/// One item's performance, in the shared Phase 15 dataset shape.
/// <para>
/// FRONTEND-PHASE15-GAP-008. <b>One contract with a subject type rather than six contracts</b> — §22
/// forbids duplicating creator analytics for guides and §23 forbids a second event analytics system,
/// and one route with a type segment is what honouring both looks like on the wire.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.ContentReadOwn)]
public sealed record GetContentAnalyticsQuery : IQuery<AnalyticsDatasetResponse>
{
    public required string ContentType { get; init; }

    public required string ContentId { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }

    public string? Period { get; init; }
}

public sealed class GetContentAnalyticsQueryHandler
    : IQueryHandler<GetContentAnalyticsQuery, AnalyticsDatasetResponse>
{
    private readonly AnalyticsQueryContext _context;
    private readonly AnalyticsReadService _reads;
    private readonly AnalyticsDatasetBuilder _builder;

    public GetContentAnalyticsQueryHandler(
        AnalyticsQueryContext context, AnalyticsReadService reads, AnalyticsDatasetBuilder builder)
    {
        _context = context;
        _reads = reads;
        _builder = builder;
    }

    public async Task<Result<AnalyticsDatasetResponse>> Handle(
        GetContentAnalyticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var contentType = AnalyticsContentTypes.FromRouteSegment(query.ContentType);

        if (contentType == AnalyticsContentType.Unknown)
        {
            return Result.Failure<AnalyticsDatasetResponse>(AnalyticsErrors.SubjectTypeUnsupported);
        }

        if (!Guid.TryParse(query.ContentId, out var contentId))
        {
            return Result.Failure<AnalyticsDatasetResponse>(AnalyticsErrors.SubjectNotFound);
        }

        var owner = await _context
            .RequireContentOwnerAsync(contentType, contentId, cancellationToken)
            .ConfigureAwait(false);

        if (owner.IsFailure)
        {
            return Result.Failure<AnalyticsDatasetResponse>(owner.Error);
        }

        var resolved = _context.ResolvePeriod(
            query.Period ?? (query.From is null ? null : "custom"),
            query.From,
            query.To,
            query.Timezone);

        if (resolved.IsFailure)
        {
            return Result.Failure<AnalyticsDatasetResponse>(resolved.Error);
        }

        var period = resolved.Value;
        var baseline = period.Previous();

        var current = await _reads
            .ReadContentAsync(contentType, contentId, period, cancellationToken)
            .ConfigureAwait(false);

        var previous = await _reads
            .ReadContentAsync(contentType, contentId, baseline, cancellationToken)
            .ConfigureAwait(false);

        var daily = await _reads
            .ReadContentDailyAsync(contentType, contentId, period, cancellationToken)
            .ConfigureAwait(false);

        // One item is one kind of content: it shows only what that kind records, and its conversions
        // have a name — a listing's sales, a home's viewings, a job's applications, an event's RSVPs.
        var metrics = AnalyticsMetricAssembler.NameConversions(
            AnalyticsMetricAssembler.BuildSet(
                AnalyticsMetricCatalog.ContentItemFor(contentType), current, previous),
            contentType);

        var dataset = _builder.Build(
            new AnalyticsSubjectResponse
            {
                Type = AnalyticsContentTypes.ToSubjectType(contentType),
                Id = contentId.ToString(),
                Label = null
            },
            period,
            baseline,
            metrics,
            AnalyticsDatasetBuilder.SeriesFrom(
                period,
                daily.ToDictionary(row => row.Day, row => (double)row.Counts.Views),
                AnalyticsMetricCatalog.ContentViews,
                _context.Calendar.Today),
            daily.Count == 0 ? null : daily.Max(row => row.ComputedAt),
            leaderboards: []);

        return Result.Success(dataset);
    }
}
