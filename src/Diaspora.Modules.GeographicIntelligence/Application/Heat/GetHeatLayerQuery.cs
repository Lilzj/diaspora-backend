using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Integration;
using Diaspora.Modules.GeographicIntelligence.Application.Intelligence;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Errors;
using Diaspora.Modules.GeographicIntelligence.Domain.Heat;
using Diaspora.Modules.GeographicIntelligence.Domain.Safety;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.Modules.GeographicIntelligence.Options;
using Diaspora.Modules.Housing.Integration;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;
using LocationCountryCode = Diaspora.Modules.Location.Domain.ValueObjects.CountryCode;

namespace Diaspora.Modules.GeographicIntelligence.Application.Heat;

/// <summary>A density layer for a viewport.</summary>
[Authorize(Policy = GeographicIntelligencePolicies.IntelligenceRead)]
public sealed record GetHeatLayerQuery : IQuery<HeatLayerResponse>
{
    /// <summary>The client's layer id: <c>housing-availability</c>, <c>jobs</c>, …</summary>
    public required string Layer { get; init; }

    /// <summary>The viewport: <c>north,south,east,west</c>. Parsed and validated by B4.</summary>
    public required string Bounds { get; init; }

    /// <summary>
    /// The rung to aggregate on, named outright. Takes precedence over <see cref="Zoom"/>.
    /// <para>
    /// Deriving the rung from the box's <em>span</em> would be the server guessing at a viewport
    /// whose pixels it cannot see, and would change the answer under a client that resized rather
    /// than zoomed. Deriving it from a zoom the client <em>stated</em> is a different thing, and is
    /// what <see cref="Zoom"/> is for.
    /// </para>
    /// </summary>
    public string? Level { get; init; }

    /// <summary>
    /// The client's map zoom, which the frontend's <c>fetchHeatLayer</c> already sends.
    /// <para>
    /// Honoured rather than ignored: the client knows its own zoom and saying so is not the server
    /// guessing. Below <see cref="CityZoomThreshold"/> a neighbourhood is smaller than a pixel and
    /// the honest rung is the city.
    /// </para>
    /// </summary>
    public double? Zoom { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>
    /// Added 21 Oct 2026. <c>24h</c>, <c>7d</c>, <c>30d</c> or <c>90d</c> — the client's
    /// <c>HeatMapTimeRange</c>. Null for no time filter. Anything else is refused.
    /// </summary>
    public string? TimeRange { get; init; }

    /// <summary>
    /// Added 21 Oct 2026. Which source a safety layer is read from: <c>community-reports</c> (the
    /// default) or <c>police-recorded-crime</c>. Never both at once. No other layer takes one.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// The zoom at or above which neighbourhood cells are legible.
    /// <para>
    /// Thirteen is roughly where a city fills a phone screen on the standard web-mercator scale.
    /// Below it a neighbourhood cluster is a pin on top of another pin.
    /// </para>
    /// </summary>
    public const double CityZoomThreshold = 12d;

    /// <summary>The rung this request implies. <see cref="Level"/> wins where both are given.</summary>
    public string? ResolvedLevel =>
        !string.IsNullOrWhiteSpace(Level)
            ? Level
            : Zoom is { } zoom
                ? zoom < CityZoomThreshold ? "City" : "Neighborhood"
                : null;
}

/// <summary>
/// Serves the density layer the frontend has been simulating since Phase 8.
/// <para>
/// <b>Every layer lands on B4's world grid.</b> The count layers take <see cref="MapCellAggregator"/>'s
/// cells as they are; the rent and safety layers bucket their own points with the same grid
/// (<see cref="MapCellAggregator.Plan"/>, <see cref="MapCellAggregator.CellOf"/>), so a cell key means
/// the same square on every layer and every pan.
/// </para>
/// <para>
/// <b>Three privacy properties, and none of them is new.</b> Every point is a square grid cell's
/// centre or the mean of three or more rows in it, never an address. A count layer counts
/// individually discoverable published content, never people. And a cell below its layer's minimum
/// sample is withheld — for counts B4's <c>Location:Spatial:MinimumClusterCellCount</c>, for a rent
/// median the same 5 homes the neighbourhood cost of living needs — and listed as withheld, with its
/// reason and without its figure, so a withheld cell is never drawn as an empty one.
/// </para>
/// <para>
/// <b>Cells are grid squares, never places.</b> A home's neighbourhood is inferred from its pin and may
/// count it, but no cell is labelled with that name: <c>areaName</c> describes the square's position
/// and nothing anybody wrote.
/// </para>
/// <para>
/// <b>An unsupported layer, source or time range is refused rather than answered empty.</b> An empty
/// heat layer renders as a quiet neighbourhood, which is a claim about the world; a named error is a
/// claim about the platform, and it is the true one.
/// </para>
/// </summary>
public sealed class GetHeatLayerQueryHandler : IQueryHandler<GetHeatLayerQuery, HeatLayerResponse>
{
    private const string PlatformSource = "Diaspora platform content";

    private const string PlatformSourceKind = "platform-content";

    private const string ListingsSourceKind = "listings";

    private readonly MapCellAggregator _cells;
    private readonly IRentStatistics _rents;
    private readonly ICommunitySafetyReports _reports;
    private readonly IPoliceCrimeData _police;
    private readonly IOptions<GeographicIntelligenceOptions> _options;
    private readonly IOptions<PoliceUkOptions> _policeOptions;
    private readonly IOptions<LocationSpatialOptions> _spatial;
    private readonly TimeProvider _clock;

    public GetHeatLayerQueryHandler(
        MapCellAggregator cells,
        IRentStatistics rents,
        ICommunitySafetyReports reports,
        IPoliceCrimeData police,
        IOptions<GeographicIntelligenceOptions> options,
        IOptions<PoliceUkOptions> policeOptions,
        IOptions<LocationSpatialOptions> spatial,
        TimeProvider clock)
    {
        _cells = cells;
        _rents = rents;
        _reports = reports;
        _police = police;
        _options = options;
        _policeOptions = policeOptions;
        _spatial = spatial;
        _clock = clock;
    }

    public async Task<Result<HeatLayerResponse>> Handle(
        GetHeatLayerQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        GeographicIntelligenceTelemetry.Requests.Add(
            1, new KeyValuePair<string, object?>("surface", "heat"));

        var definition = HeatLayerCatalog.Find(request.Layer);

        if (definition is null)
        {
            return Result.Failure<HeatLayerResponse>(
                GeographicIntelligenceErrors.LayerUnsupported(
                    request.Layer ?? string.Empty,
                    string.Join(", ", HeatLayerCatalog.SupportedLayers)));
        }

        (string Name, TimeSpan Span)? range = null;

        if (!string.IsNullOrWhiteSpace(request.TimeRange))
        {
            range = HeatTimeRange.Parse(request.TimeRange);

            if (range is null)
            {
                return Result.Failure<HeatLayerResponse>(
                    GeographicIntelligenceErrors.HeatTimeRangeInvalid(
                        request.TimeRange, string.Join(", ", HeatTimeRange.Supported)));
            }
        }

        var source = ResolveSource(definition, request.Source);

        if (source.IsFailure)
        {
            return Result.Failure<HeatLayerResponse>(source.Error);
        }

        var span = SpanOf(request.Bounds);

        if (span is null)
        {
            return Result.Failure<HeatLayerResponse>(GeographicIntelligenceErrors.BoundsInvalid);
        }

        var maximum = source.Value == HeatLayerCatalog.PoliceRecordedCrimeSource
            ? Math.Min(_options.Value.MaximumHeatSpanDegrees, _policeOptions.Value.MaximumHeatSpanDegrees)
            : _options.Value.MaximumHeatSpanDegrees;

        if (span > maximum)
        {
            GeographicIntelligenceTelemetry.BoundsRejected.Add(
                1, new KeyValuePair<string, object?>("surface", "heat"));

            return Result.Failure<HeatLayerResponse>(
                GeographicIntelligenceErrors.BoundsTooLarge(span.Value, maximum));
        }

        var needsCountry = definition.Kind is HeatLayerKind.RentMedian or HeatLayerKind.RentIndex
            || source.Value == HeatLayerCatalog.PoliceRecordedCrimeSource;

        var country = string.IsNullOrWhiteSpace(request.CountryCode)
            ? null
            : LocationCountryCode.TryCreate(request.CountryCode) is { IsSuccess: true } code
                ? code.Value.Value
                : null;

        if (needsCountry && country is null)
        {
            return Result.Failure<HeatLayerResponse>(GeographicIntelligenceErrors.HeatCountryRequired(definition.Layer));
        }

        var context = new HeatContext(request, definition, range, _clock.GetUtcNow(), country);

        var result = definition.Kind switch
        {
            HeatLayerKind.Count => await CountLayerAsync(context, cancellationToken),
            HeatLayerKind.RentMedian or HeatLayerKind.RentIndex => await RentLayerAsync(context, cancellationToken),
            _ => source.Value == HeatLayerCatalog.PoliceRecordedCrimeSource
                ? await PoliceLayerAsync(context, cancellationToken)
                : await CommunitySafetyLayerAsync(context, cancellationToken)
        };

        if (result.IsFailure)
        {
            GeographicIntelligenceTelemetry.Errors.Add(
                1, new KeyValuePair<string, object?>("code", result.Error.Code));

            return result;
        }

        GeographicIntelligenceTelemetry.CellsSuppressed.Add(
            result.Value.SuppressedCells, new KeyValuePair<string, object?>("layer", definition.Layer));

        return result;
    }

    // ── Counts: B4's cells ───────────────────────────────────────────────────────────────────────

    private async Task<Result<HeatLayerResponse>> CountLayerAsync(
        HeatContext context, CancellationToken cancellationToken)
    {
        var definition = context.Definition;
        var now = context.Now;

        // An event's time is when it starts, so its window looks forward: events starting before the
        // range ends, those already under way included. Everything else looks back from now.
        var events = definition.TimeBasis == "starts";

        DateTimeOffset? from = context.Range is { } window && !events ? now - window.Span : null;
        DateTimeOffset? to = context.Range is { } ahead ? (events ? now + ahead.Span : now) : null;

        // One resource type, so a single-layer map over twenty cells is twenty counts rather than
        // a hundred and forty. Narrowing here is what makes the layer affordable at all.
        var page = await _cells.AggregateAsync(
            context.Request.Bounds,
            context.Request.ResolvedLevel,
            [definition.ResourceType!],
            context.Request.CountryCode,
            cellSizeKm: null,
            from,
            to,
            cancellationToken);

        if (page.IsFailure)
        {
            return Result.Failure<HeatLayerResponse>(page.Error);
        }

        var minimum = page.Value.MinimumCellCount;

        var cells = page.Value.Cells
            .Select(cell =>
            {
                var count = cell.Counts.TryGetValue(definition.ResourceType!, out var value) ? value : 0;

                return new HeatCell(
                    new MapCellFrame(cell.Key, cell.Id, cell.South, cell.West, cell.North, cell.East, cell.Label),
                    cell.Latitude,
                    cell.Longitude,
                    count,
                    count,
                    null);
            })
            .ToList();

        var withheld = page.Value.WithheldCells
            .Select(frame => Withheld(
                frame,
                "BelowMinimumCount",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Fewer than {0} {1} in this cell, so it is not drawn. Withheld is not zero.",
                    minimum,
                    definition.Unit)))
            .ToList();

        var windowLine = context.Range is { } named
            ? events
                ? $"Only events starting in the next {Describe(named.Span)} are counted, and events already under way."
                : $"Only {definition.Unit} published in the last {Describe(named.Span)} are counted."
            : null;

        var limitations = new List<string>
        {
            $"Counts {definition.Unit} published on Diaspora, not what exists in the area.",
            "Each point is a grid cell's centre, or the mean of three or more rows in it, never an "
            + "address. No individual record is identifiable from this layer.",
            "Rows without coordinates are not counted.",
            $"Cells with fewer than {minimum} are withheld; {withheld.Count} were withheld here."
        };

        if (windowLine is not null)
        {
            limitations.Add(windowLine);
        }

        return Result.Success(Compose(
            context,
            sourceKind: PlatformSourceKind,
            sourceName: PlatformSource,
            unit: definition.Unit,
            cells,
            withheld,
            level: page.Value.Level.ToString(),
            cellSizeDegrees: page.Value.CellSizeDegrees,
            minimumCellCount: minimum,
            truncated: page.Value.Truncated,
            bounds: (page.Value.Bounds.North, page.Value.Bounds.South, page.Value.Bounds.East, page.Value.Bounds.West),
            methodology:
                $"Live count of published {definition.Unit} per square grid cell inside the viewport, "
                + "from each row's own coordinates, computed on request. Intensity is scaled to the "
                + "highest cell in this viewport, so it is comparable within this layer and not across "
                + "layers or across viewports.",
            limitations,
            from: context.Range is null ? null : events ? now : from,
            to: to,
            timeBasis: definition.TimeBasis));
    }

    // ── Rents: Housing's sample on B4's grid ─────────────────────────────────────────────────────

    private async Task<Result<HeatLayerResponse>> RentLayerAsync(
        HeatContext context, CancellationToken cancellationToken)
    {
        var plan = MapCellAggregator.Plan(context.Request.Bounds, context.Request.ResolvedLevel);

        if (plan.IsFailure)
        {
            return Result.Failure<HeatLayerResponse>(plan.Error);
        }

        var options = _options.Value;
        var now = context.Now;
        var index = context.Definition.Kind == HeatLayerKind.RentIndex;

        DateTimeOffset? from = context.Range is { } window ? now - window.Span : null;
        DateTimeOffset? to = context.Range is null ? null : now;

        var sample = await _rents.SampleMonthlyRentsAsync(context.Country!, from, to, cancellationToken);

        var grid = plan.Value;
        var box = grid.Box;
        var buckets = new Dictionary<(long Row, long Column), RentBucket>();

        foreach (var point in sample.Points)
        {
            if (!Inside(box.North, box.South, box.East, box.West, point.Latitude, point.Longitude))
            {
                continue;
            }

            var key = MapCellAggregator.CellOf(grid.CellSizeDegrees, point.Latitude, point.Longitude);

            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new RentBucket();
                buckets[key] = bucket;
            }

            bucket.Add(point);
        }

        var cellMinimum = options.MinimumRentListingsInArea;
        var countryMinimum = options.MinimumRentListingsInCountry;

        var baseline = sample.CountryMedianMonthlyRent is > 0m
            && sample.CountrySampleSize >= countryMinimum
                ? sample.CountryMedianMonthlyRent
                : null;

        var cells = new List<HeatCell>();
        var withheld = new List<WithheldHeatCellResponse>();

        foreach (var ((row, column), bucket) in buckets)
        {
            var frame = MapCellAggregator.Frame(grid.Exponent, row, column);

            if (index && baseline is null)
            {
                withheld.Add(Withheld(
                    frame,
                    "CountrySampleTooSmall",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The country has {0} {1} to rent in this sample and an index needs at least {2} "
                        + "to compare against, so no cell is indexed yet.",
                        sample.CountrySampleSize,
                        sample.CountrySampleSize == 1 ? "home" : "homes",
                        countryMinimum)));
                continue;
            }

            if (bucket.Rents.Count < cellMinimum)
            {
                withheld.Add(Withheld(
                    frame,
                    "TooFewHomes",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Fewer than {0} homes to rent in this cell, so no median is published. "
                        + "Withheld is not zero.",
                        cellMinimum)));
                continue;
            }

            var median = RentArithmetic.Median(bucket.Rents)!.Value;

            var value = index
                ? (int)Math.Round(100m * median / baseline!.Value, MidpointRounding.AwayFromZero)
                : (int)Math.Round(median, 0, MidpointRounding.AwayFromZero);

            var (latitude, longitude) = MapCellAggregator.Marker(
                frame, bucket.Rents.Count, bucket.LatitudeSum, bucket.LongitudeSum);

            cells.Add(new HeatCell(frame, latitude, longitude, value, bucket.Rents.Count, null));
        }

        var unavailable = index && baseline is null && sample.CountrySampleSize < countryMinimum
            ? "CountrySampleTooSmall"
            : sample.Currency is null
                ? "NoRentsListed"
                : null;

        var limitations = new List<string>
        {
            "Asking rents of homes listed on Diaspora, not agreed rents and not a survey of the area.",
            "Housing only. Food, transport, utilities, healthcare and leisure are not measured, so "
            + "there is no overall cost-of-living figure.",
            "A median of few listings moves a lot when one home is added or let.",
            "Each point is a grid cell's centre, or the mean of the homes in it as strangers are shown "
            + "them, never an address. A home's neighbourhood may be inferred from its pin to count it, "
            + "and no cell is named after one.",
            string.Format(
                CultureInfo.InvariantCulture,
                "Cells with fewer than {0} homes are withheld; {1} were withheld here.",
                cellMinimum,
                withheld.Count)
        };

        if (sample.OtherCurrencyListings > 0)
        {
            limitations.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} priced in a currency other than {2} and left out rather than converted.",
                sample.OtherCurrencyListings,
                sample.OtherCurrencyListings == 1 ? "listing is" : "listings are",
                sample.Currency));
        }

        if (context.Range is { } named)
        {
            limitations.Add(index
                ? $"Only homes published in the last {Describe(named.Span)} are counted, in each cell and in the country's baseline alike."
                : $"Only homes published in the last {Describe(named.Span)} are counted.");
        }

        var methodology = index
            ? string.Format(
                CultureInfo.InvariantCulture,
                "The median monthly asking rent of the homes to rent listed on Diaspora in each grid cell, "
                + "as an index against the median of the {0} listed across the country (100 = the same), "
                + "in {1}. Weekly, quarterly and yearly rents are converted to monthly; nightly prices "
                + "are left out. The same index, arithmetic and minimums as a neighbourhood's cost of living.",
                sample.CountrySampleSize,
                sample.Currency ?? "the country's currency")
            : string.Format(
                CultureInfo.InvariantCulture,
                "The median monthly asking rent, in {0}, of the homes to rent listed on Diaspora in each "
                + "grid cell. Weekly, quarterly and yearly rents are converted to monthly; nightly prices "
                + "are left out. Intensity is scaled to the dearest cell in this viewport.",
                sample.Currency ?? "the country's currency");

        return Result.Success(Compose(
            context,
            sourceKind: ListingsSourceKind,
            sourceName: GetCostOfLivingQueryHandler.ListingsSource,
            unit: context.Definition.Unit,
            cells,
            withheld,
            level: grid.Level.ToString(),
            cellSizeDegrees: grid.CellSizeDegrees,
            minimumCellCount: cellMinimum,
            truncated: sample.IsTruncated,
            bounds: (box.North, box.South, box.East, box.West),
            methodology,
            limitations,
            from,
            to,
            timeBasis: context.Definition.TimeBasis) with
        {
            IsAvailable = unavailable is null,
            UnavailableReason = unavailable,
            Currency = sample.Currency,
            MinimumCountrySample = index ? countryMinimum : null
        });
    }

    // ── Safety: one source at a time ─────────────────────────────────────────────────────────────

    private async Task<Result<HeatLayerResponse>> CommunitySafetyLayerAsync(
        HeatContext context, CancellationToken cancellationToken)
    {
        var plan = MapCellAggregator.Plan(context.Request.Bounds, context.Request.ResolvedLevel);

        if (plan.IsFailure)
        {
            return Result.Failure<HeatLayerResponse>(plan.Error);
        }

        var grid = plan.Value;
        var box = grid.Box;
        var now = context.Now;

        // Without a range, the safety read's own window, so the map and a neighbourhood page agree.
        var span = context.Range?.Span ?? TimeSpan.FromDays(_options.Value.SafetyReportWindowDays);
        var from = now - span;

        var reports = await _reports.ListCorroboratedPointsAsync(
            box.South, box.West, box.North, box.East, from, now, cancellationToken);

        var points = reports.Points
            .Select(point => (point.Latitude, point.Longitude, point.Category))
            .ToList();

        var (cells, withheld, minimum) = CountCells(grid, points, "reports");

        var limitations = new List<string>
        {
            "Diaspora does not publish a safety score. This counts what one source recorded and says "
            + "nothing about how safe anywhere is.",
            "Community reports are alerts members filed and other members corroborated. Many incidents "
            + "are never reported on Diaspora, and a quiet cell may simply have few members.",
            "Each point is a grid cell's centre, or the mean of three or more reports in it.",
            "Reports without a pin are not on the map.",
            $"Cells with fewer than {minimum} reports are withheld; {withheld.Count} were withheld here."
        };

        if (reports.IsLowerBound)
        {
            limitations.Add("More reports were inside this viewport than one request reads, so the counts are lower bounds.");
        }

        return Result.Success(Compose(
            context,
            sourceKind: HeatLayerCatalog.CommunityReportsSource,
            sourceName: GetSafetyScoreQueryHandler.CommunitySource,
            unit: "reports",
            cells,
            withheld,
            level: grid.Level.ToString(),
            cellSizeDegrees: grid.CellSizeDegrees,
            minimumCellCount: minimum,
            truncated: reports.IsLowerBound,
            bounds: (box.North, box.South, box.East, box.West),
            methodology: string.Format(
                CultureInfo.InvariantCulture,
                "Police and Emergency alerts members published over the last {0} that other members "
                + "corroborated enough for Diaspora to mark them community-verified, counted per square "
                + "grid cell at each alert's own pin. Alerts their authors withdrew, and anything "
                + "moderators removed, are not counted. Intensity is scaled to the busiest cell in this "
                + "viewport.",
                Describe(span)),
            limitations,
            from,
            now,
            timeBasis: "published"));
    }

    private async Task<Result<HeatLayerResponse>> PoliceLayerAsync(
        HeatContext context, CancellationToken cancellationToken)
    {
        var plan = MapCellAggregator.Plan(context.Request.Bounds, context.Request.ResolvedLevel);

        if (plan.IsFailure)
        {
            return Result.Failure<HeatLayerResponse>(plan.Error);
        }

        var grid = plan.Value;
        var box = grid.Box;
        var now = context.Now;

        const string Method =
            "Street-level crimes recorded by the police, as data.police.uk places them at anonymised "
            + "points, counted per square grid cell over whole published months. Intensity is scaled to "
            + "the busiest cell in this viewport.";

        var limitations = new List<string>
        {
            "Diaspora does not publish a safety score. This counts what one source recorded and says "
            + "nothing about how safe anywhere is.",
            "Police-recorded crime covers England, Wales and Northern Ireland only, counts what was "
            + "reported to and recorded by the police, and places each crime at an approximate, "
            + "anonymised point.",
            "The source publishes each month some weeks after it ends, so a recent time range may hold "
            + "no police data yet."
        };

        if (!_police.Covers(context.Country!))
        {
            return Result.Success(Unavailable(
                context, grid, HeatLayerCatalog.PoliceRecordedCrimeSource,
                GetSafetyScoreQueryHandler.NoPoliceSource, "crimes", PoliceCrimeUnavailable.NotPublished,
                "Diaspora publishes police-recorded crime where an official source publishes it openly and "
                + "allows it to be republished. No such source covers this country, so there is no layer "
                + "here — which is not a low one.",
                limitations, licensed: false, timeBasis: "month"));
        }

        DateTimeOffset? from = context.Range is { } window ? now - window.Span : null;
        DateTimeOffset? to = context.Range is null ? null : now;

        var answer = await _police.ListInBoxAsync(
            box.South, box.West, box.North, box.East, from, to, cancellationToken);

        if (answer.UnavailableReason is null && answer.Points.Count == 0)
        {
            // A whole viewport of nothing is how the source answers a place it does not cover —
            // Scotland, for one — so it is said as no coverage rather than drawn as no crime.
            answer = PoliceCrimePointsAnswer.Unavailable(PoliceCrimeUnavailable.NoCoverage, answer.NewestPublishedMonth);
        }

        if (answer.UnavailableReason is { } reason)
        {
            var detail = reason switch
            {
                PoliceCrimeUnavailable.NotYetPublished => string.Format(
                    CultureInfo.InvariantCulture,
                    "data.police.uk has published no month inside this time range yet; its newest month is {0}.",
                    answer.NewestPublishedMonth),
                PoliceCrimeUnavailable.TooManyToRead =>
                    "data.police.uk refuses an area holding more than 10,000 crimes in a month. Zoom in.",
                PoliceCrimeUnavailable.NoCoverage =>
                    "data.police.uk placed no crime inside this viewport for those months, which is how it "
                    + "answers a place it does not cover. It is not a count of zero.",
                PoliceCrimeUnavailable.Disabled => "This deployment does not read data.police.uk.",
                _ => "data.police.uk could not be read just now."
            };

            return Result.Success(Unavailable(
                context, grid, HeatLayerCatalog.PoliceRecordedCrimeSource,
                GetSafetyScoreQueryHandler.PoliceSource, "crimes", reason, $"{Method} {detail}",
                limitations, licensed: true, timeBasis: "month"));
        }

        var points = answer.Points
            .Where(point => Inside(box.North, box.South, box.East, box.West, point.Latitude, point.Longitude))
            .Select(point => (point.Latitude, point.Longitude, point.Category))
            .ToList();

        var (cells, withheld, minimum) = CountCells(grid, points, "crimes");

        limitations.Add($"Cells with fewer than {minimum} crimes are withheld; {withheld.Count} were withheld here.");

        return Result.Success(Compose(
            context,
            sourceKind: HeatLayerCatalog.PoliceRecordedCrimeSource,
            sourceName: GetSafetyScoreQueryHandler.PoliceSource,
            unit: "crimes",
            cells,
            withheld,
            level: grid.Level.ToString(),
            cellSizeDegrees: grid.CellSizeDegrees,
            minimumCellCount: minimum,
            truncated: false,
            bounds: (box.North, box.South, box.East, box.West),
            methodology: string.Format(
                CultureInfo.InvariantCulture,
                "{0} Months counted: {1} to {2}.",
                Method,
                answer.FirstMonth,
                answer.LastMonth),
            limitations,
            MonthStart(answer.FirstMonth),
            MonthStart(answer.LastMonth)?.AddMonths(1).AddTicks(-1),
            timeBasis: "month") with
        {
            Licence = GetSafetyScoreQueryHandler.PoliceLicence,
            Attribution = GetSafetyScoreQueryHandler.PoliceAttribution
        });
    }

    /// <summary>Counts category-tagged points per cell, under B4's floor.</summary>
    private (List<HeatCell> Cells, List<WithheldHeatCellResponse> Withheld, int Minimum) CountCells(
        MapGridPlan grid, IReadOnlyList<(double Latitude, double Longitude, string Category)> points, string unit)
    {
        var minimum = Math.Max(1, _spatial.Value.MinimumClusterCellCount);
        var buckets = new Dictionary<(long Row, long Column), CountBucket>();

        foreach (var point in points)
        {
            var key = MapCellAggregator.CellOf(grid.CellSizeDegrees, point.Latitude, point.Longitude);

            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new CountBucket();
                buckets[key] = bucket;
            }

            bucket.Add(point.Latitude, point.Longitude, point.Category);
        }

        var cells = new List<HeatCell>();
        var withheld = new List<WithheldHeatCellResponse>();

        foreach (var ((row, column), bucket) in buckets)
        {
            var frame = MapCellAggregator.Frame(grid.Exponent, row, column);

            if (bucket.Total < minimum)
            {
                withheld.Add(Withheld(
                    frame,
                    "BelowMinimumCount",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Fewer than {0} {1} in this cell, so it is not drawn. Withheld is not zero.",
                        minimum,
                        unit)));
                continue;
            }

            var (latitude, longitude) = MapCellAggregator.Marker(
                frame, bucket.Total, bucket.LatitudeSum, bucket.LongitudeSum);

            cells.Add(new HeatCell(frame, latitude, longitude, bucket.Total, bucket.Total, bucket.Categories));
        }

        return (cells, withheld, minimum);
    }

    // ── Composition ──────────────────────────────────────────────────────────────────────────────

    private HeatLayerResponse Compose(
        HeatContext context,
        string sourceKind,
        string sourceName,
        string unit,
        List<HeatCell> cells,
        List<WithheldHeatCellResponse> withheld,
        string level,
        double cellSizeDegrees,
        int minimumCellCount,
        bool truncated,
        (double North, double South, double East, double West) bounds,
        string methodology,
        IReadOnlyList<string> limitations,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string timeBasis)
    {
        var ordered = cells
            .OrderByDescending(cell => cell.Value)
            .ThenBy(cell => cell.Frame.Key, StringComparer.Ordinal)
            .ToList();

        var minimum = ordered.Count == 0 ? 0 : ordered.Min(cell => cell.Value);
        var peak = ordered.Count == 0 ? 0 : ordered.Max(cell => cell.Value);
        var now = context.Now;

        return new HeatLayerResponse
        {
            Layer = context.Definition.Layer,
            Source = sourceKind,
            Unit = unit,
            Points =
            [
                .. ordered.Select(cell => new HeatPointResponse
                {
                    Latitude = cell.Latitude,
                    Longitude = cell.Longitude,
                    Intensity = Normalize(cell.Value, minimum, peak),
                    RawValue = cell.Value,
                    AreaName = cell.Frame.Label,
                    AreaId = cell.Frame.Id,
                    CellKey = cell.Frame.Key,
                    CellBounds = new BoundsResponse
                    {
                        North = cell.Frame.North,
                        South = cell.Frame.South,
                        East = cell.Frame.East,
                        West = cell.Frame.West
                    },
                    SampleSize = cell.SampleSize,
                    Categories = cell.Categories
                })
            ],

            // Always fitted. An absolute scale needs a platform-wide maximum, and every candidate
            // for that number is a constant nobody measured.
            Scale = "relative-to-viewport",
            MinValue = minimum,
            MaxValue = peak,
            Level = level,
            CellSizeDegrees = cellSizeDegrees,
            MinimumCellCount = minimumCellCount,
            SuppressedCells = withheld.Count,
            WithheldCells = [.. withheld.OrderBy(cell => cell.CellKey, StringComparer.Ordinal)],
            Truncated = truncated,
            TimeRange = context.Range?.Name,
            TimeBasis = timeBasis,
            From = from,
            To = to,
            Bounds = new BoundsResponse
            {
                North = bounds.North,
                South = bounds.South,
                East = bounds.East,
                West = bounds.West
            },
            Freshness = new DataFreshnessResponse
            {
                ComputedAt = now,
                SourceName = sourceName,
                Methodology = methodology,
                Limitations = limitations,
                ValidUntil = now.AddHours(_options.Value.FreshnessWindowHours),
                SampleSize = ordered.Sum(cell => cell.SampleSize)
            }
        };
    }

    private HeatLayerResponse Unavailable(
        HeatContext context,
        MapGridPlan grid,
        string sourceKind,
        string sourceName,
        string unit,
        string reason,
        string methodology,
        IReadOnlyList<string> limitations,
        bool licensed,
        string timeBasis) =>
        Compose(
            context,
            sourceKind,
            sourceName,
            unit,
            [],
            [],
            grid.Level.ToString(),
            grid.CellSizeDegrees,
            Math.Max(1, _spatial.Value.MinimumClusterCellCount),
            truncated: false,
            (grid.Box.North, grid.Box.South, grid.Box.East, grid.Box.West),
            methodology,
            limitations,
            from: null,
            to: null,
            timeBasis) with
        {
            IsAvailable = false,
            UnavailableReason = reason,
            Licence = licensed ? GetSafetyScoreQueryHandler.PoliceLicence : null,
            Attribution = licensed ? GetSafetyScoreQueryHandler.PoliceAttribution : null
        };

    private static WithheldHeatCellResponse Withheld(MapCellFrame frame, string reason, string detail) =>
        new()
        {
            CellKey = frame.Key,
            AreaId = frame.Id,
            AreaName = frame.Label,
            CellBounds = new BoundsResponse
            {
                North = frame.North,
                South = frame.South,
                East = frame.East,
                West = frame.West
            },
            Reason = reason,
            Detail = detail
        };

    private static Result<string?> ResolveSource(HeatLayerDefinition definition, string? requested)
    {
        var given = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();

        if (definition.Kind != HeatLayerKind.SafetyCount)
        {
            return given is null
                ? Result.Success<string?>(null)
                : Result.Failure<string?>(
                    GeographicIntelligenceErrors.HeatSourceUnsupported(definition.Layer, given, string.Empty));
        }

        if (given is null)
        {
            return Result.Success<string?>(HeatLayerCatalog.CommunityReportsSource);
        }

        var match = HeatLayerCatalog.SafetySources.FirstOrDefault(source =>
            string.Equals(source, given, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? Result.Failure<string?>(GeographicIntelligenceErrors.HeatSourceUnsupported(
                definition.Layer, given, string.Join(", ", HeatLayerCatalog.SafetySources)))
            : Result.Success<string?>(match);
    }

    private static bool Inside(double north, double south, double east, double west, double latitude, double longitude) =>
        latitude >= south && latitude <= north
        && (west > east
            ? longitude >= west || longitude <= east
            : longitude >= west && longitude <= east);

    private static string Describe(TimeSpan span) =>
        span.TotalHours <= 24
            ? string.Create(CultureInfo.InvariantCulture, $"{span.TotalHours:0} hours")
            : string.Create(CultureInfo.InvariantCulture, $"{span.TotalDays:0} days");

    private static DateTimeOffset? MonthStart(string? month) =>
        month is not null && DateTimeOffset.TryParseExact(
            month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start)
            ? start
            : null;

    /// <summary>
    /// Latitude span of the requested box, or null when it does not parse.
    /// <para>
    /// Latitude only, deliberately. Longitude span is ambiguous across the antimeridian — a box
    /// from 179°E to 179°W is two degrees wide, and subtracting says it is 358 — so a longitude
    /// check here would refuse the one viewport B4 went out of its way to support.
    /// </para>
    /// </summary>
    private static double? SpanOf(string? bounds)
    {
        var parts = bounds?.Split(',');

        if (parts is not { Length: 4 })
        {
            return null;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var north)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var south))
        {
            return null;
        }

        return double.IsFinite(north) && double.IsFinite(south) ? Math.Abs(north - south) : null;
    }

    /// <summary>
    /// Scales a value onto 0–1 against this viewport's own range.
    /// <para>
    /// When every cell holds the same number there is no range to scale against, and the honest
    /// answer is that they are all equally busy — 1, not 0. Returning 0 would draw a uniformly
    /// active district as empty.
    /// </para>
    /// </summary>
    private static double Normalize(int value, int minimum, int peak) =>
        peak <= minimum ? 1d : Math.Round((value - minimum) / (double)(peak - minimum), 4);

    private sealed record HeatContext(
        GetHeatLayerQuery Request,
        HeatLayerDefinition Definition,
        (string Name, TimeSpan Span)? Range,
        DateTimeOffset Now,
        string? Country);

    private sealed record HeatCell(
        MapCellFrame Frame,
        double Latitude,
        double Longitude,
        int Value,
        int SampleSize,
        IReadOnlyDictionary<string, int>? Categories);

    private sealed class RentBucket
    {
        public List<decimal> Rents { get; } = [];

        public double LatitudeSum { get; private set; }

        public double LongitudeSum { get; private set; }

        public void Add(RentPoint point)
        {
            Rents.Add(point.MonthlyRent);
            LatitudeSum += point.Latitude;
            LongitudeSum += point.Longitude;
        }
    }

    private sealed class CountBucket
    {
        public Dictionary<string, int> Categories { get; } = new(StringComparer.Ordinal);

        public int Total { get; private set; }

        public double LatitudeSum { get; private set; }

        public double LongitudeSum { get; private set; }

        public void Add(double latitude, double longitude, string category)
        {
            Categories[category] = Categories.TryGetValue(category, out var count) ? count + 1 : 1;
            Total++;
            LatitudeSum += latitude;
            LongitudeSum += longitude;
        }
    }
}
