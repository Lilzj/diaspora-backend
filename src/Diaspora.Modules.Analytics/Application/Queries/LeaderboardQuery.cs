using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// One board: a subject's items ranked by one counted figure over a period.
/// <para>
/// <b>Items, never people.</b> A row names a guide, a listing, a home, a job or a profile and the
/// figure it earned. The boards are the ones the owner datasets already carry
/// (<c>creator-content</c>, <c>seller-listings</c>, <c>landlord-listings</c>, <c>employer-jobs</c>,
/// <c>provider-profiles</c>), plus <c>top-content</c> for any owner subject and
/// <c>platform-content</c> for platform readers.
/// </para>
/// <para>
/// <b>Only additive counts rank.</b> Views, saves, shares, comments, helpful votes, enquiries and
/// actions taken are summed from the daily rollups. A distinct-viewer count does not sum across days
/// and is not kept per item per period; a rate would put an item with one view and one save above one
/// with a thousand of each. Both are refused with the reason rather than approximated.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record GetAnalyticsLeaderboardQuery : IQuery<AnalyticsLeaderboardResponse>
{
    public required string LeaderboardKey { get; init; }

    public string? SubjectType { get; init; }

    public string? SubjectId { get; init; }

    /// <summary>The client's metric key. <c>content.views</c> when absent.</summary>
    public string? Metric { get; init; }

    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }

    /// <summary>1 to 50; 10 when absent.</summary>
    public int? Take { get; init; }

    /// <summary>The <c>nextCursor</c> of the previous page.</summary>
    public string? Cursor { get; init; }
}

public sealed class GetAnalyticsLeaderboardQueryHandler
    : IQueryHandler<GetAnalyticsLeaderboardQuery, AnalyticsLeaderboardResponse>
{
    private const int DefaultTake = 10;
    private const int MaximumTake = 50;

    /// <summary>The boards: key → (the subject type it ranks, its heading). Null means any owner subject.</summary>
    private static readonly Dictionary<string, (string? SubjectType, string Label)> Boards =
        new(StringComparer.Ordinal)
        {
            ["top-content"] = (null, "Top content"),
            ["creator-content"] = ("creator", "Top content"),
            ["seller-listings"] = ("seller", "Top listings"),
            ["landlord-listings"] = ("landlord", "Top listings"),
            ["employer-jobs"] = ("employer", "Top jobs"),
            ["provider-profiles"] = ("provider", "Top profiles"),
            ["platform-content"] = ("platform", "Top content on Diaspora")
        };

    /// <summary>The figures a board ranks by, keyed by the client's metric key.</summary>
    private static readonly Dictionary<string, (string Label, Func<AnalyticsItemTotals, long> Read)> Rankable =
        new(StringComparer.Ordinal)
        {
            ["content.views"] = ("Views", item => item.Views),
            ["content.saves"] = ("Saves", item => item.Saves),
            ["content.shares"] = ("Shares", item => item.Shares),
            ["content.comments"] = ("Comments", item => item.Comments),
            ["content.helpfulVotes"] = ("Helpful votes", item => item.HelpfulVotes),
            ["content.enquiries"] = ("Enquiries", item => item.Enquiries),
            ["conversion.count"] = ("Actions taken", item => item.Conversions)
        };

    private readonly AnalyticsQueryContext _context;
    private readonly AnalyticsActivitySubjects _subjects;
    private readonly AnalyticsItemReads _items;
    private readonly IAnalyticsContentDimensionRepository _dimensions;

    public GetAnalyticsLeaderboardQueryHandler(
        AnalyticsQueryContext context,
        AnalyticsActivitySubjects subjects,
        AnalyticsItemReads items,
        IAnalyticsContentDimensionRepository dimensions)
    {
        _context = context;
        _subjects = subjects;
        _items = items;
        _dimensions = dimensions;
    }

    public async Task<Result<AnalyticsLeaderboardResponse>> Handle(
        GetAnalyticsLeaderboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsLeaderboardResponse>(caller.Error);
        }

        var key = query.LeaderboardKey?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!Boards.TryGetValue(key, out var board))
        {
            return Result.Failure<AnalyticsLeaderboardResponse>(AnalyticsErrors.LeaderboardUnknown(key));
        }

        var requestedType = query.SubjectType?.Trim().ToLowerInvariant() is { Length: > 0 } given ? given : null;

        if (board.SubjectType is not null && requestedType is not null && requestedType != board.SubjectType)
        {
            return Result.Failure<AnalyticsLeaderboardResponse>(AnalyticsErrors.LeaderboardSubjectMismatch);
        }

        var metricKey = query.Metric?.Trim() is { Length: > 0 } metric ? metric : "content.views";

        if (!Rankable.TryGetValue(metricKey, out var ranked))
        {
            return Result.Failure<AnalyticsLeaderboardResponse>(AnalyticsErrors.LeaderboardMetricUnsupported(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A board cannot rank by \"{metricKey}\". Boards rank by counted figures that add up across days: {string.Join(", ", Rankable.Keys)}. Distinct viewers are not kept per item per period, and a rate would rank one view with one save above a thousand of each.")));
        }

        var resolved = _context.ResolvePeriod(
            query.Period ?? (query.From is null ? null : "custom"), query.From, query.To, query.Timezone);

        if (resolved.IsFailure)
        {
            return Result.Failure<AnalyticsLeaderboardResponse>(resolved.Error);
        }

        var subject = await _subjects
            .ResolveAsync(
                requestedType ?? board.SubjectType,
                query.SubjectId,
                caller.Value,
                allowItems: false,
                "creator",
                cancellationToken)
            .ConfigureAwait(false);

        if (subject.IsFailure)
        {
            return Result.Failure<AnalyticsLeaderboardResponse>(subject.Error);
        }

        if (subject.Value.Kind == AnalyticsActivitySubjectKind.Content)
        {
            return Result.Failure<AnalyticsLeaderboardResponse>(AnalyticsErrors.SubjectUnsupportedBecause(
                "One item ranks nothing. Ask for the owner's board instead."));
        }

        var period = resolved.Value;
        var baseline = period.Previous();

        var current = await _items.ReadAsync(subject.Value, period, cancellationToken).ConfigureAwait(false);
        var previous = (await _items.ReadAsync(subject.Value, baseline, cancellationToken).ConfigureAwait(false))
            .ToDictionary(item => (item.ContentType, item.ContentId), item => ranked.Read(item));

        var ordered = current
            .Where(item => ranked.Read(item) > 0)
            .OrderByDescending(item => ranked.Read(item))
            .ThenByDescending(item => item.Views)
            .ThenBy(item => item.ContentId)
            .ToList();

        var offset = int.TryParse(query.Cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
        var take = Math.Clamp(query.Take ?? DefaultTake, 1, MaximumTake);
        var page = ordered.Skip(offset).Take(take).ToList();
        var hasMore = offset + page.Count < ordered.Count;

        var dimensions = page.Count == 0
            ? new Dictionary<(Domain.Enums.AnalyticsContentType, Guid), Domain.Dimensions.AnalyticsContentDimension>()
            : (await _dimensions
                    .ListAsync(page.Select(item => (item.ContentType, item.ContentId)).ToList(), cancellationToken)
                    .ConfigureAwait(false))
                .ToDictionary(dimension => (dimension.ContentType, dimension.ContentId));

        var byViews = metricKey == "content.views";
        var baselineFrom = baseline.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var baselineTo = baseline.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var rows = page
            .Select((item, index) =>
            {
                dimensions.TryGetValue((item.ContentType, item.ContentId), out var dimension);

                var value = ranked.Read(item);
                var before = previous.TryGetValue((item.ContentType, item.ContentId), out var earlier) ? earlier : 0;

                return new AnalyticsLeaderboardRowResponse
                {
                    Position = offset + index + 1,
                    Entity = new AnalyticsEntityRefResponse
                    {
                        EntityType = AnalyticsContentTypes.ToSubjectType(item.ContentType),
                        EntityId = item.ContentId.ToString(),
                        Title = dimension?.Title ?? "Content",
                        Route = AnalyticsContentTypes.ToClientRoute(item.ContentType, item.ContentId)
                    },
                    Value = value,
                    Format = "count",
                    SecondaryValue = byViews ? item.Engagements : item.Views,
                    SecondaryFormat = "count",
                    SecondaryLabel = byViews ? "Engagements" : "Views",
                    Comparison = new AnalyticsComparisonResponse
                    {
                        Basis = "previousPeriod",
                        BaselineFrom = baselineFrom,
                        BaselineTo = baselineTo,
                        BaselineValue = before,
                        ChangePercent = before == 0 ? null : (value - before) / (double)before,
                        ChangeAbsolute = value - before,
                        UnavailableReason = before == 0 ? "zeroBaseline" : null
                    }
                };
            })
            .ToList();

        return Result.Success(new AnalyticsLeaderboardResponse
        {
            Key = key,
            Label = board.Label,
            MetricKey = metricKey,
            Rows = rows,
            NextCursor = hasMore
                ? (offset + page.Count).ToString(CultureInfo.InvariantCulture)
                : null,
            HasMore = hasMore,
            Source = "backend"
        });
    }
}
