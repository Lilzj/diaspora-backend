using System.Globalization;
using System.Text.Json;
using Diaspora.Modules.Analytics.Contracts.V1.Requests;
using Diaspora.Modules.Analytics.Domain.Reports;

namespace Diaspora.Modules.Analytics.Application.Services;

/// <summary>
/// Checks and stores the client's <c>AnalyticsQuery</c> for saved reports and exports.
/// <para>
/// <b>Normalised before it is kept.</b> The query is rebuilt from its known fields rather than stored
/// as sent, so a saved report carries nothing the client's type does not name, and every list and
/// string on it is bounded — a definition column that took arbitrary JSON would be a free-text store
/// with a report's name on it.
/// </para>
/// </summary>
public static class AnalyticsQueryPayloads
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The client's <c>AnalyticsSubjectType</c> union, exactly.</summary>
    public static readonly IReadOnlySet<string> SubjectTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "platform", "creator", "country", "community", "neighborhood", "provider", "employer",
        "seller", "landlord", "organization", "campaign", "job", "listing", "housing", "guide",
        "event", "update"
    };

    /// <summary>The client's <c>AnalyticsVisualization</c> union, exactly.</summary>
    public static readonly IReadOnlySet<string> Visualizations = new HashSet<string>(StringComparer.Ordinal)
    {
        "metricCards", "line", "bar", "area", "donut", "funnel", "table", "leaderboard"
    };

    private const int MaximumListItems = 50;
    private const int MaximumFilterValues = 50;
    private const int MaximumTokenLength = 96;
    private const int MaximumIdentifierLength = 128;
    private const int MaximumLabelLength = 200;

    /// <summary>
    /// The query, normalised, or the reason it cannot be kept.
    /// </summary>
    public static (AnalyticsQueryPayload? Query, string? Problem) Normalise(AnalyticsQueryPayload? query)
    {
        if (query is null)
        {
            return (null, "A report needs a query: a subject and a period.");
        }

        var subjectType = query.Subject?.Type?.Trim();

        if (string.IsNullOrEmpty(subjectType) || !SubjectTypes.Contains(subjectType))
        {
            return (null, "The query's subject is not something this platform reports on.");
        }

        var subjectId = Clip(query.Subject?.Id, MaximumIdentifierLength);

        var range = query.Range;

        if (range is null
            || !IsDate(range.From)
            || !IsDate(range.To)
            || string.IsNullOrWhiteSpace(range.Timezone))
        {
            return (null, "The query's period needs a start date, an end date and a time zone.");
        }

        var lists = new[] { query.Metrics, query.Breakdowns };

        if (lists.Any(list => list is { Count: > MaximumListItems })
            || query.Filters is { Count: > MaximumListItems }
            || query.Filters?.Any(filter => filter.Values is { Count: > MaximumFilterValues }) == true)
        {
            return (null, "The query asks for more figures or filters than a report can hold.");
        }

        return (new AnalyticsQueryPayload
        {
            Subject = new AnalyticsSubjectPayload
            {
                Type = subjectType,
                Id = subjectId,
                Label = Clip(query.Subject?.Label, MaximumLabelLength)
            },
            Metrics = Tokens(query.Metrics),
            Range = new AnalyticsRangePayload
            {
                Preset = Clip(range.Preset, MaximumTokenLength),
                From = range.From!.Trim(),
                To = range.To!.Trim(),
                Timezone = Clip(range.Timezone, MaximumTokenLength),
                IsDeviceTimezone = range.IsDeviceTimezone ?? false
            },
            Granularity = Clip(query.Granularity, MaximumTokenLength),
            Comparison = Clip(query.Comparison, MaximumTokenLength),
            Filters = query.Filters?
                .Where(filter => !string.IsNullOrWhiteSpace(filter.Dimension))
                .Select(filter => new AnalyticsFilterPayload
                {
                    Dimension = Clip(filter.Dimension, MaximumTokenLength),
                    Values = Tokens(filter.Values, MaximumIdentifierLength)
                })
                .ToList() ?? [],
            Breakdowns = Tokens(query.Breakdowns),
            IncludeSeries = query.IncludeSeries ?? false,
            CommunityScopeToken = Clip(query.CommunityScopeToken, MaximumLabelLength)
        }, null);
    }

    public static string Serialise(AnalyticsQueryPayload query) => JsonSerializer.Serialize(query, Json);

    /// <summary>Whether the stored form fits the column. Checked before saving, not discovered by it.</summary>
    public static bool Fits(string json) => json.Length <= AnalyticsSavedReport.DefinitionMaximumLength;

    public static AnalyticsQueryPayload Deserialise(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AnalyticsQueryPayload>(json, Json) ?? new AnalyticsQueryPayload();
        }
        catch (JsonException)
        {
            return new AnalyticsQueryPayload();
        }
    }

    private static bool IsDate(string? value) =>
        DateOnly.TryParseExact(
            value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static string? Clip(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= length ? trimmed : trimmed[..length];
    }

    private static List<string> Tokens(IReadOnlyList<string>? values, int length = MaximumTokenLength) =>
        values?
            .Select(value => Clip(value, length))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
}
