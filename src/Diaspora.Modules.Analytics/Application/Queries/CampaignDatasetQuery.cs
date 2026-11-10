using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Giving;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Queries;

/// <summary>
/// One fundraising campaign's giving over a period, for the people who run it.
/// <para>
/// Served at <c>GET /api/v1/analytics/campaigns/{id}</c> in the shared Phase 15 dataset shape. The
/// figures are counted by the module that owns campaigns and donations, through
/// <see cref="ICampaignAnalyticsSource"/>, which also decides who may read them: whoever may run the
/// campaign, and staff. Anybody else — a donor included — receives the same 404 as for a campaign
/// that does not exist.
/// </para>
/// <para>
/// <b>Giving only.</b> Visits to a campaign page and shares of it are not recorded, so anything that
/// needs them — a visit-to-gift rate above all — is named in <c>unavailableMetrics</c> rather than
/// approximated.
/// </para>
/// </summary>
[RequirePermission(AnalyticsPermissions.CreatorRead)]
public sealed record GetCampaignAnalyticsDatasetQuery : IQuery<AnalyticsDatasetResponse>
{
    public required string CampaignId { get; init; }

    public string? Period { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }
}

public sealed class GetCampaignAnalyticsDatasetQueryHandler
    : IQueryHandler<GetCampaignAnalyticsDatasetQuery, AnalyticsDatasetResponse>
{
    internal const string RaisedKey = "wallet.donations";
    internal const string GiftsKey = "conversion.count";
    internal const string SupportersKey = "campaign.supporters";
    internal const string RefundedKey = "revenue.refunded";
    internal const string RaisedToDateKey = "campaign.raisedToDate";
    internal const string ProgressKey = "campaign.progress";

    /// <summary>What a campaign dashboard could ask for and this platform does not record.</summary>
    internal static readonly IReadOnlyList<string> NotRecorded =
    [
        "content.views",
        "content.uniqueViewers",
        "content.shares",
        "conversion.rate"
    ];

    private readonly AnalyticsQueryContext _context;
    private readonly IEnumerable<ICampaignAnalyticsSource> _sources;

    public GetCampaignAnalyticsDatasetQueryHandler(
        AnalyticsQueryContext context, IEnumerable<ICampaignAnalyticsSource> sources)
    {
        _context = context;
        _sources = sources;
    }

    public async Task<Result<AnalyticsDatasetResponse>> Handle(
        GetCampaignAnalyticsDatasetQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsDatasetResponse>(caller.Error);
        }

        var resolved = _context.ResolvePeriod(
            query.Period ?? (query.From is null ? null : "custom"), query.From, query.To, query.Timezone);

        if (resolved.IsFailure)
        {
            return Result.Failure<AnalyticsDatasetResponse>(resolved.Error);
        }

        if (!Guid.TryParse(query.CampaignId, out var campaignId)
            || _sources.FirstOrDefault() is not { } source)
        {
            return Result.Failure<AnalyticsDatasetResponse>(AnalyticsErrors.SubjectNotFound);
        }

        var period = resolved.Value;
        var baseline = period.Previous();

        var reading = await source
            .ReadAsync(
                campaignId,
                caller.Value,
                _context.IsPlatformReader,
                baseline.Start,
                period.Start,
                period.End,
                cancellationToken)
            .ConfigureAwait(false);

        // Not there, and not yours to read, answer alike.
        if (reading is null || !reading.ViewerMayRead)
        {
            return Result.Failure<AnalyticsDatasetResponse>(AnalyticsErrors.SubjectNotFound);
        }

        var zone = AnalyticsReportingCalendar.TryResolve(period.TimeZoneId) ?? _context.Calendar.Zone;
        var now = _context.Calendar.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        var limitations = new List<string>
        {
            "Visits to a campaign page and shares of it are not recorded, so there is no visit count and no visit-to-gift rate.",
            "A gift counts on the day its payment completed, not the day it was pledged.",
            "Supporters are distinct named donors plus each anonymous gift once: an anonymous gift carries no identity to tell it from another."
        };

        if (!reading.HasCause)
        {
            limitations.Add("This campaign has not been published, so it has had nowhere to receive gifts.");
        }

        var freshness = new DataFreshnessResponse
        {
            ComputedAt = now,
            SourceName = "Diaspora giving",
            Methodology = string.Create(
                CultureInfo.InvariantCulture,
                $"Counted at request time from the campaign's completed and refunded gifts, in {reading.Currency}. Days are cut in {period.TimeZoneId}."),
            Limitations = limitations
        };

        var unavailable = NotRecorded.ToList();
        var current = reading.Current;
        var previous = reading.Baseline;

        var metrics = new List<AnalyticsMetricResponse>
        {
            MoneyMetric(RaisedKey, "Raised", current.CompletedMinorUnits, previous.CompletedMinorUnits, true, reading, baseline, freshness),
            CountMetric(GiftsKey, "Gifts", current.CompletedCount, previous.CompletedCount, true, baseline, freshness),
            CountMetric(SupportersKey, "Supporters", current.SupporterCount, previous.SupporterCount, true, baseline, freshness),
            MoneyMetric(RefundedKey, "Refunded", current.RefundedMinorUnits, previous.RefundedMinorUnits, false, reading, baseline, freshness),
            new()
            {
                Key = RaisedToDateKey,
                Label = "Raised to date",
                Value = new SourcedValueResponse { Value = Major(reading.RaisedToDateMinorUnits, reading.Exponent), Source = "backend" },
                Format = "currency",
                Computation = "server-aggregate",
                HigherIsBetter = true,
                Comparison = null,
                Freshness = freshness,
                PrivacyClass = "financial",
                Money = AnalyticsMoneyResponse.From(
                    reading.RaisedToDateMinorUnits, reading.Currency, reading.Exponent, reading.CurrencySymbol)
            }
        };

        if (reading.TargetMinorUnits is long target && target > 0)
        {
            metrics.Add(new AnalyticsMetricResponse
            {
                Key = ProgressKey,
                Label = "Toward the target",
                Value = new SourcedValueResponse
                {
                    Value = reading.RaisedToDateMinorUnits / (double)target,
                    Source = "backend"
                },
                Format = "percentage",
                Computation = "server-aggregate",
                HigherIsBetter = true,
                Comparison = null,
                Freshness = freshness,
                PrivacyClass = "financial"
            });
        }
        else
        {
            unavailable.Add(ProgressKey);
            limitations.Add("This campaign set no target, so there is no progress toward one.");
        }

        var raisedDaily = reading.CurrentGifts
            .GroupBy(gift => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(gift.CompletedAt, zone).DateTime))
            .ToDictionary(group => group.Key, group => Major(group.Sum(gift => gift.AmountMinorUnits), reading.Exponent));

        var giftsDaily = reading.CurrentGifts
            .GroupBy(gift => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(gift.CompletedAt, zone).DateTime))
            .ToDictionary(group => group.Key, group => (double)group.Count());

        var granularity = AnalyticsMetricAssembler.GranularityName(period.Granularity);

        var series = new List<AnalyticsSeriesResponse>
        {
            new()
            {
                Key = RaisedKey,
                Label = "Raised",
                Granularity = granularity,
                Format = "currency",
                Currency = reading.Currency,
                Points = Points(period, raisedDaily, today),
                Source = "backend"
            },
            new()
            {
                Key = GiftsKey,
                Label = "Gifts",
                Granularity = granularity,
                Format = "count",
                Points = Points(period, giftsDaily, today),
                Source = "backend"
            }
        };

        return Result.Success(new AnalyticsDatasetResponse
        {
            Subject = new AnalyticsSubjectResponse
            {
                Type = "campaign",
                Id = reading.CampaignId.ToString(),
                Label = reading.Title
            },
            Range = new AnalyticsTimeRangeResponse
            {
                Preset = AnalyticsQueryContext.ToDatasetPreset(period.Preset),
                From = period.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                To = period.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Timezone = period.TimeZoneId,
                IsDeviceTimezone = false
            },
            Comparison = "previousPeriod",
            Status = "PARTIAL",
            Metrics = metrics,
            Series = series,
            Breakdowns = [],
            Funnels = [],
            Leaderboards = [],
            Insights = [],
            Freshness = freshness,
            UnavailableMetrics = unavailable
        });
    }

    private static List<AnalyticsPointResponse> Points(
        AnalyticsPeriod period, IReadOnlyDictionary<DateOnly, double> daily, DateOnly today) =>
        AnalyticsMetricAssembler.Bucket(period, daily, today)
            .Select(point => new AnalyticsPointResponse
            {
                At = point.At,
                Value = point.Value,
                IsPartialBucket = point.IsPartialBucket
            })
            .ToList();

    private static double Major(long minorUnits, int exponent) =>
        (double)(minorUnits / (decimal)Math.Pow(10, exponent));

    private static AnalyticsComparisonResponse Compare(
        double current, double previous, AnalyticsPeriod baseline) => new()
    {
        Basis = "previousPeriod",
        BaselineFrom = baseline.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        BaselineTo = baseline.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        BaselineValue = previous,
        ChangePercent = previous == 0 ? null : (current - previous) / Math.Abs(previous),
        ChangeAbsolute = current - previous,
        UnavailableReason = previous == 0 ? "zeroBaseline" : null
    };

    private static AnalyticsMetricResponse CountMetric(
        string key,
        string label,
        long current,
        long previous,
        bool higherIsBetter,
        AnalyticsPeriod baseline,
        DataFreshnessResponse freshness) => new()
    {
        Key = key,
        Label = label,
        Value = new SourcedValueResponse { Value = current, Source = "backend" },
        Format = "count",
        Computation = "server-aggregate",
        HigherIsBetter = higherIsBetter,
        Comparison = Compare(current, previous, baseline),
        Freshness = freshness,
        PrivacyClass = "financial"
    };

    private static AnalyticsMetricResponse MoneyMetric(
        string key,
        string label,
        long currentMinor,
        long previousMinor,
        bool higherIsBetter,
        CampaignAnalyticsReading reading,
        AnalyticsPeriod baseline,
        DataFreshnessResponse freshness)
    {
        var current = Major(currentMinor, reading.Exponent);
        var previous = Major(previousMinor, reading.Exponent);

        return new AnalyticsMetricResponse
        {
            Key = key,
            Label = label,
            Value = new SourcedValueResponse { Value = current, Source = "backend" },
            Format = "currency",
            Computation = "server-aggregate",
            HigherIsBetter = higherIsBetter,
            Comparison = Compare(current, previous, baseline),
            Freshness = freshness,
            PrivacyClass = "financial",
            Money = AnalyticsMoneyResponse.From(currentMinor, reading.Currency, reading.Exponent, reading.CurrencySymbol)
        };
    }
}
