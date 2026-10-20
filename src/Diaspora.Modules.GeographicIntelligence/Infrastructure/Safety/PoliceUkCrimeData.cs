using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Diaspora.Modules.GeographicIntelligence.Domain.Safety;
using Diaspora.Modules.GeographicIntelligence.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.GeographicIntelligence.Infrastructure.Safety;

/// <summary>
/// Street-level crime counts from data.police.uk.
/// <para>
/// <b>What the source is.</b> Police forces in England, Wales and Northern Ireland publish every
/// recorded street crime monthly, located to an anonymised point, under the Open Government Licence.
/// <c>crimes-street/all-crime?lat=&amp;lng=&amp;date=</c> returns the crimes within one mile of a point
/// for one month; <c>crimes-street-dates</c> lists the months published; <c>crime-categories</c> names
/// the categories. There is no count endpoint, so each month's crimes are read and counted here.
/// </para>
/// <para>
/// <b>All months or none.</b> A period that quietly came back shorter than it says would understate
/// the count, so one unreadable month makes the whole answer "could not be read".
/// </para>
/// <para>
/// <b>A year of nothing is no coverage.</b> The source holds no street-level data for Scotland and
/// answers a Scottish point with empty months. Within a mile of a populated place a whole year without
/// a single recorded crime does not happen, so that answer is reported as no coverage rather than
/// published as a zero somebody would read as "safe".
/// </para>
/// <para>
/// Counts are cached per point and month, because a published month is rarely revised and the source
/// asks callers to go gently.
/// </para>
/// </summary>
public sealed class PoliceUkCrimeData : IPoliceCrimeData
{
    public const string HttpClientName = "police-uk";

    private const int MaximumCacheEntries = 4096;

    private static readonly TimeSpan PublishedMonthsLifetime = TimeSpan.FromHours(12);

    /// <summary>
    /// Viewport reads kept at once. Small and separate from the per-point cache, because one viewport
    /// month can hold thousands of crimes and a heat map asks again on every pan.
    /// </summary>
    private const int MaximumCachedViewports = 64;

    private readonly IHttpClientFactory _clients;
    private readonly IOptions<PoliceUkOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<PoliceUkCrimeData> _logger;

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, CacheEntry> _viewports = new(StringComparer.Ordinal);

    public PoliceUkCrimeData(
        IHttpClientFactory clients,
        IOptions<PoliceUkOptions> options,
        TimeProvider clock,
        ILogger<PoliceUkCrimeData> logger)
    {
        _clients = clients;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public bool Covers(string countryCode) =>
        string.Equals(countryCode, "GB", StringComparison.OrdinalIgnoreCase);

    public async Task<PoliceCrimeAnswer> CountAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            return PoliceCrimeAnswer.Unavailable(PoliceCrimeUnavailable.Disabled);
        }

        try
        {
            var months = (await PublishedMonthsAsync(cancellationToken)).Take(options.Months).ToList();

            if (months.Count == 0)
            {
                return PoliceCrimeAnswer.Unavailable(PoliceCrimeUnavailable.SourceUnavailable);
            }

            var names = await CategoryNamesAsync(months[0], cancellationToken);

            using var gate = new SemaphoreSlim(options.MaxConcurrentRequests);

            var perMonth = await Task.WhenAll(months.Select(async month =>
            {
                await gate.WaitAsync(cancellationToken);

                try
                {
                    return await MonthAsync(latitude, longitude, month, cancellationToken);
                }
                finally
                {
                    gate.Release();
                }
            }));

            if (perMonth.Any(counts => counts is null))
            {
                return PoliceCrimeAnswer.Unavailable(PoliceCrimeUnavailable.SourceUnavailable);
            }

            var totals = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var counts in perMonth)
            {
                foreach (var (category, count) in counts!)
                {
                    totals[category] = totals.GetValueOrDefault(category) + count;
                }
            }

            if (totals.Values.Sum() == 0)
            {
                return PoliceCrimeAnswer.Unavailable(PoliceCrimeUnavailable.NoCoverage);
            }

            return PoliceCrimeAnswer.Of(new PoliceCrimeCounts
            {
                FirstMonth = months[^1],
                LastMonth = months[0],
                Months = months.Count,
                ByCategory = totals,
                CategoryNames = names
            });
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "data.police.uk could not be read for a safety count.");
            return PoliceCrimeAnswer.Unavailable(PoliceCrimeUnavailable.SourceUnavailable);
        }
    }

    public async Task<PoliceCrimePointsAnswer> ListInBoxAsync(
        double south,
        double west,
        double north,
        double east,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            return PoliceCrimePointsAnswer.Unavailable(PoliceCrimeUnavailable.Disabled);
        }

        try
        {
            var published = await PublishedMonthsAsync(cancellationToken);

            if (published.Count == 0)
            {
                return PoliceCrimePointsAnswer.Unavailable(PoliceCrimeUnavailable.SourceUnavailable);
            }

            List<string> months = from is null && to is null
                ? [published[0]]
                : published.Where(month => Overlaps(month, from, to)).ToList();

            if (months.Count == 0)
            {
                return PoliceCrimePointsAnswer.Unavailable(PoliceCrimeUnavailable.NotYetPublished, published[0]);
            }

            var names = await CategoryNamesAsync(months[0], cancellationToken);
            var points = new List<PoliceCrimePoint>();

            // One month at a time: a viewport month is the heaviest read this source serves.
            foreach (var month in months)
            {
                var read = await ViewportMonthAsync(south, west, north, east, month, cancellationToken);

                if (read.TooMany)
                {
                    return PoliceCrimePointsAnswer.Unavailable(PoliceCrimeUnavailable.TooManyToRead, published[0]);
                }

                if (read.Points is null)
                {
                    return PoliceCrimePointsAnswer.Unavailable(PoliceCrimeUnavailable.SourceUnavailable, published[0]);
                }

                points.AddRange(read.Points);
            }

            return new PoliceCrimePointsAnswer
            {
                Points = points,
                FirstMonth = months[^1],
                LastMonth = months[0],
                Months = months.Count,
                CategoryNames = names,
                NewestPublishedMonth = published[0]
            };
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "data.police.uk could not be read for a safety heat layer.");
            return PoliceCrimePointsAnswer.Unavailable(PoliceCrimeUnavailable.SourceUnavailable);
        }
    }

    /// <summary>Whether a published month overlaps [from, to]. Either end may be open.</summary>
    private static bool Overlaps(string month, DateTimeOffset? from, DateTimeOffset? to)
    {
        if (!DateTimeOffset.TryParseExact(
                month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start))
        {
            return false;
        }

        var end = start.AddMonths(1);

        return (from is null || end > from.Value) && (to is null || start <= to.Value);
    }

    /// <summary>
    /// One month's crimes inside the viewport, by the source's polygon search. <c>TooMany</c> when the
    /// source refuses the area as too large (it answers 503 above 10,000 crimes); null points when
    /// it could not be read.
    /// </summary>
    private async Task<(IReadOnlyList<PoliceCrimePoint>? Points, bool TooMany)> ViewportMonthAsync(
        double south, double west, double north, double east, string month, CancellationToken cancellationToken)
    {
        static string At(double latitude, double longitude) =>
            string.Create(CultureInfo.InvariantCulture, $"{latitude:F5},{longitude:F5}");

        var poly = string.Join(':', At(north, west), At(north, east), At(south, east), At(south, west));
        var key = $"viewport:{poly}:{month}";

        if (_viewports.TryGetValue(key, out var cached)
            && cached.Expires > _clock.GetUtcNow()
            && cached.Value is IReadOnlyList<PoliceCrimePoint> hit)
        {
            return (hit, false);
        }

        var client = _clients.CreateClient(HttpClientName);
        var options = _options.Value;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));

        var uri = new Uri(
            new Uri(options.BaseAddress, UriKind.Absolute),
            $"crimes-street/all-crime?poly={poly}&date={month}");

        using var response = await client.GetAsync(uri, timeout.Token);

        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            return (null, true);
        }

        if (!response.IsSuccessStatusCode)
        {
            return (null, false);
        }

        await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return (null, false);
        }

        var points = new List<PoliceCrimePoint>();

        foreach (var crime in document.RootElement.EnumerateArray())
        {
            // The source sends coordinates as strings. A crime it could not place is not on a map.
            if (crime.TryGetProperty("category", out var category) && category.GetString() is { Length: > 0 } id
                && crime.TryGetProperty("location", out var location) && location.ValueKind == JsonValueKind.Object
                && Coordinate(location, "latitude") is { } latitude
                && Coordinate(location, "longitude") is { } longitude)
            {
                points.Add(new PoliceCrimePoint(latitude, longitude, id, month));
            }
        }

        if (_viewports.Count >= MaximumCachedViewports)
        {
            _viewports.Clear();
        }

        _viewports[key] = new CacheEntry(_clock.GetUtcNow() + TimeSpan.FromHours(options.CacheHours), points);

        return (points, false);
    }

    private static double? Coordinate(JsonElement location, string name)
    {
        if (!location.TryGetProperty(name, out var value))
        {
            return null;
        }

        var parsed = value.ValueKind switch
        {
            JsonValueKind.String when double.TryParse(
                value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var text) => text,
            JsonValueKind.Number => value.GetDouble(),
            _ => (double?)null
        };

        return parsed is { } number && double.IsFinite(number) ? number : null;
    }

    /// <summary>The published months, newest first: <c>2026-06</c>, <c>2026-05</c>, …</summary>
    private async Task<IReadOnlyList<string>> PublishedMonthsAsync(CancellationToken cancellationToken)
    {
        if (TryCached<IReadOnlyList<string>>("dates", out var cached))
        {
            return cached;
        }

        using var document = await GetJsonAsync("crimes-street-dates", cancellationToken);

        var months = document.RootElement.EnumerateArray()
            .Select(entry => entry.TryGetProperty("date", out var date) ? date.GetString() : null)
            .OfType<string>()
            .Where(IsMonth)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(month => month, StringComparer.Ordinal)
            .ToList();

        Cache("dates", months, PublishedMonthsLifetime);

        return months;
    }

    /// <summary>The source's own category names, keyed by identifier.</summary>
    private async Task<IReadOnlyDictionary<string, string>> CategoryNamesAsync(
        string month, CancellationToken cancellationToken)
    {
        var key = $"categories:{month}";

        if (TryCached<IReadOnlyDictionary<string, string>>(key, out var cached))
        {
            return cached;
        }

        using var document = await GetJsonAsync($"crime-categories?date={month}", cancellationToken);

        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            if (entry.TryGetProperty("url", out var url) && entry.TryGetProperty("name", out var name)
                && url.GetString() is { Length: > 0 } id && name.GetString() is { Length: > 0 } label)
            {
                names[id] = label;
            }
        }

        Cache(key, names, TimeSpan.FromHours(_options.Value.CacheHours));

        return names;
    }

    /// <summary>One month's crimes within a mile of the point, counted per category. Null when unreadable.</summary>
    private async Task<Dictionary<string, int>?> MonthAsync(
        double latitude, double longitude, string month, CancellationToken cancellationToken)
    {
        var lat = latitude.ToString("F6", CultureInfo.InvariantCulture);
        var lng = longitude.ToString("F6", CultureInfo.InvariantCulture);
        var key = $"crimes:{latitude.ToString("F4", CultureInfo.InvariantCulture)}:{longitude.ToString("F4", CultureInfo.InvariantCulture)}:{month}";

        if (TryCached<Dictionary<string, int>>(key, out var cached))
        {
            return cached;
        }

        using var document = await GetJsonAsync(
            $"crimes-street/all-crime?lat={lat}&lng={lng}&date={month}", cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var crime in document.RootElement.EnumerateArray())
        {
            if (crime.TryGetProperty("category", out var category) && category.GetString() is { Length: > 0 } id)
            {
                counts[id] = counts.GetValueOrDefault(id) + 1;
            }
        }

        Cache(key, counts, TimeSpan.FromHours(_options.Value.CacheHours));

        return counts;
    }

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var client = _clients.CreateClient(HttpClientName);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));

        var uri = new Uri(new Uri(options.BaseAddress, UriKind.Absolute), path);

        using var response = await client.GetAsync(uri, timeout.Token);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);

        return await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token);
    }

    private static bool IsMonth(string value) =>
        DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private bool TryCached<T>(string key, out T value)
        where T : class
    {
        if (_cache.TryGetValue(key, out var entry) && entry.Expires > _clock.GetUtcNow() && entry.Value is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    private void Cache(string key, object value, TimeSpan lifetime)
    {
        if (_cache.Count >= MaximumCacheEntries)
        {
            // Bounded the simple way: a full cache starts again. Every entry is re-readable.
            _cache.Clear();
        }

        _cache[key] = new CacheEntry(_clock.GetUtcNow() + lifetime, value);
    }

    private sealed record CacheEntry(DateTimeOffset Expires, object Value);
}
