using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Services;

/// <summary>
/// What distinguishes one owner dashboard from another: which content it sums, which figures it
/// shows, and what it calls things.
/// </summary>
/// <param name="ContentType">The one type of content the dashboard sums. Nothing else is counted.</param>
/// <param name="Headline">The metric keys served, in tile order.</param>
/// <param name="SubjectType">The dataset's subject type, as the client names it.</param>
/// <param name="LeaderboardKey">The board's stable key.</param>
/// <param name="LeaderboardLabel">The board's heading.</param>
/// <param name="ConversionLabel">
/// What an action taken is called on this dashboard — "Sales", "Viewings", "Applications" — on its
/// tile and beside each item on the board.
/// </param>
/// <param name="ConversionRateLabel">What actions taken per view are called on this dashboard.</param>
/// <param name="FallbackTitle">An item's title when its publication was never recorded.</param>
public sealed record AnalyticsOwnerDatasetProfile(
    AnalyticsContentType ContentType,
    IReadOnlyList<string> Headline,
    string SubjectType,
    string LeaderboardKey,
    string LeaderboardLabel,
    string ConversionLabel,
    string ConversionRateLabel,
    string FallbackTitle)
{
    /// <summary>A marketplace seller: their listings, their sales.</summary>
    public static AnalyticsOwnerDatasetProfile Seller { get; } = new(
        AnalyticsContentType.Marketplace,
        AnalyticsMetricCatalog.SellerHeadline,
        "seller",
        "seller-listings",
        "Top listings",
        "Sales",
        "Conversion rate",
        "Listing");

    /// <summary>A landlord: their homes, the viewings booked on them.</summary>
    public static AnalyticsOwnerDatasetProfile Landlord { get; } = new(
        AnalyticsContentType.Housing,
        AnalyticsMetricCatalog.LandlordHeadline,
        "landlord",
        "landlord-listings",
        "Top listings",
        "Viewings",
        "Viewing rate",
        "Home");

    /// <summary>An employer: their jobs, the applications made to them.</summary>
    public static AnalyticsOwnerDatasetProfile Employer { get; } = new(
        AnalyticsContentType.Job,
        AnalyticsMetricCatalog.EmployerHeadline,
        "employer",
        "employer-jobs",
        "Top jobs",
        "Applications",
        "Application rate",
        "Job");
}

/// <summary>
/// The caller's own content of one type, summed into the shared Phase 15 dataset.
/// <para>
/// <b>One implementation for every owner dashboard.</b> A seller's listings, a landlord's homes and
/// an employer's jobs are the same question asked of a different content type — per-item rollups
/// summed to the member who owns them — and three copies of the answer would drift the first time
/// one was fixed. A provider dashboard would be the same question again.
/// </para>
/// <para>
/// <b>The subject is always the caller.</b> The caller's <em>user</em> id is the only identity this
/// reads by, never a seller, landlord or employer profile id: analytics keys every row to the member,
/// and a dashboard asked by profile id would answer nothing for everybody. There is no parameter that
/// names anyone, so there is no parameter to change.
/// </para>
/// </summary>
public sealed class AnalyticsOwnerDatasetComposer
{
    /// <summary>How many items the top-items board carries.</summary>
    private const int TopItemRows = 10;

    private readonly AnalyticsQueryContext _context;
    private readonly AnalyticsReadService _reads;
    private readonly AnalyticsDatasetBuilder _builder;
    private readonly IAnalyticsContentDimensionRepository _dimensions;

    public AnalyticsOwnerDatasetComposer(
        AnalyticsQueryContext context,
        AnalyticsReadService reads,
        AnalyticsDatasetBuilder builder,
        IAnalyticsContentDimensionRepository dimensions)
    {
        _context = context;
        _reads = reads;
        _builder = builder;
        _dimensions = dimensions;
    }

    public async Task<Result<AnalyticsDatasetResponse>> ComposeAsync(
        AnalyticsOwnerDatasetProfile profile,
        string? period,
        string? from,
        string? to,
        string? timezone,
        bool includeSeries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsDatasetResponse>(caller.Error);
        }

        var resolved = _context.ResolvePeriod(
            period ?? (from is null ? null : "custom"),
            from,
            to,
            timezone);

        if (resolved.IsFailure)
        {
            return Result.Failure<AnalyticsDatasetResponse>(resolved.Error);
        }

        var window = resolved.Value;
        var baseline = window.Previous();

        var current = await _reads
            .ReadOwnerContentTypeAsync(caller.Value, profile.ContentType, window, cancellationToken)
            .ConfigureAwait(false);

        var previous = await _reads
            .ReadOwnerContentTypeAsync(caller.Value, profile.ContentType, baseline, cancellationToken)
            .ConfigureAwait(false);

        // The catalogue calls a conversion "Actions taken", because one catalogue serves every kind
        // of content. On a dashboard about one kind, the action has a name — a sale, a viewing, an
        // application — and a member reading "Conversions" beside their homes has to be told which.
        // Only the label changes: the key, the definition and the arithmetic stay the catalogue's,
        // and the sentence naming a withheld rate reads the label, so it says "Viewing rate" too.
        var metrics = AnalyticsMetricAssembler.BuildSet(profile.Headline, current, previous)
            .Select(metric => metric.Key switch
            {
                AnalyticsMetricCatalog.Conversion => metric with { Label = profile.ConversionLabel },
                AnalyticsMetricCatalog.ConversionRate => metric with { Label = profile.ConversionRateLabel },
                _ => metric
            })
            .ToList();

        var rows = await _reads
            .ReadOwnerContentTypeDailyAsync(caller.Value, profile.ContentType, window, cancellationToken)
            .ConfigureAwait(false);

        // Summed per day across the owner's items. A day with no activity has no rows, and the
        // bucketer walks the calendar asking for each day, so the gap is drawn as a gap.
        var series = includeSeries
            ? AnalyticsDatasetBuilder.SeriesFrom(
                window,
                rows.GroupBy(row => row.Day)
                    .ToDictionary(group => group.Key, group => (double)group.Sum(row => row.Counts.Views)),
                AnalyticsMetricCatalog.ContentViews,
                _context.Calendar.Today)
            : [];

        var leaderboard = await BuildTopItemsAsync(profile, rows, cancellationToken).ConfigureAwait(false);

        return Result.Success(_builder.Build(
            new AnalyticsSubjectResponse { Type = profile.SubjectType, Id = null, Label = null },
            window,
            baseline,
            metrics,
            series,
            rows.Count == 0 ? null : rows.Max(row => row.ComputedAt),
            leaderboard));
    }

    /// <summary>
    /// The owner's best-performing items for the period.
    /// <para>
    /// <b>Ranked by the server, echoed by the client</b>, with actions taken beside views so an item
    /// that is seen a great deal and never acted on is visible as exactly that. The rows name items,
    /// never the people who bought, booked or applied.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<AnalyticsLeaderboardResponse>> BuildTopItemsAsync(
        AnalyticsOwnerDatasetProfile profile,
        IReadOnlyList<ContentDailyRollup> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var folded = rows
            .GroupBy(row => row.ContentId)
            .Select(group => new
            {
                ContentId = group.Key,
                Views = group.Sum(row => row.Counts.Views),
                Conversions = group.Sum(row => row.Counts.Conversions)
            })
            .Where(entry => entry.Views > 0 || entry.Conversions > 0)
            .OrderByDescending(entry => entry.Views)
            .ThenByDescending(entry => entry.Conversions)
            .ThenBy(entry => entry.ContentId)
            .Take(TopItemRows)
            .ToList();

        if (folded.Count == 0)
        {
            return [];
        }

        var dimensions = (await _dimensions
                .ListAsync(
                    folded.Select(entry => (profile.ContentType, entry.ContentId)).ToList(),
                    cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(dimension => dimension.ContentId);

        var definition = AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.ContentViews)!;

        var leaderboardRows = folded
            .Select((entry, index) =>
            {
                dimensions.TryGetValue(entry.ContentId, out var dimension);

                return new AnalyticsLeaderboardRowResponse
                {
                    Position = index + 1,
                    Entity = new AnalyticsEntityRefResponse
                    {
                        EntityType = AnalyticsContentTypes.ToSubjectType(profile.ContentType),
                        EntityId = entry.ContentId.ToString(),
                        Title = dimension?.Title ?? profile.FallbackTitle,
                        Route = AnalyticsContentTypes.ToClientRoute(profile.ContentType, entry.ContentId)
                    },
                    Value = entry.Views,
                    Format = "count",
                    SecondaryValue = entry.Conversions,
                    SecondaryFormat = "count",
                    SecondaryLabel = profile.ConversionLabel
                };
            })
            .ToList();

        return
        [
            new AnalyticsLeaderboardResponse
            {
                Key = profile.LeaderboardKey,
                Label = profile.LeaderboardLabel,
                MetricKey = definition.CatalogueKey,
                Rows = leaderboardRows,
                NextCursor = null,
                HasMore = false,
                Source = "backend"
            }
        ];
    }
}
