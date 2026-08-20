using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>One catalogue node inside a viewport, with what every vertical has in it.</summary>
/// <param name="Area">The node. Guaranteed to have a centre — one without cannot be a cell.</param>
/// <param name="Counts">Per-resource counts. An absent key means that vertical could not be asked.</param>
/// <param name="Total">The sum across the counted verticals.</param>
public sealed record MapCell(AdministrativeArea Area, IReadOnlyDictionary<string, int> Counts, int Total);

/// <summary>The cells for a viewport, with everything a caller needs to describe what it got.</summary>
public sealed record MapCellPage
{
    public required IReadOnlyList<MapCell> Cells { get; init; }

    public required BoundingBox Bounds { get; init; }

    public required AdministrativeLevel Level { get; init; }

    public required IReadOnlyList<string> ResourceTypes { get; init; }

    public required int MinimumCellCount { get; init; }

    public required int SuppressedCells { get; init; }

    public required bool Truncated { get; init; }
}

/// <summary>
/// Aggregates a viewport into catalogue nodes with counts against them.
/// <para>
/// <b>Extracted from <c>GetMapClustersQueryHandler</c> so a second consumer could exist without a
/// second implementation.</b> B21's density layer answers a different question in a different
/// shape, and it needs exactly this: the same cells, the same counters, the same cap and — the part
/// that matters — <b>the same suppression floor</b>. A density layer with its own privacy threshold
/// is a second threshold somebody has to remember to raise, and the one thing worse than an
/// unsuppressed heat map is two suppression rules that disagree.
/// </para>
/// <para>
/// Extracting rather than letting the new module send the existing query is what the platform's own
/// analyzer requires: a handler that depends on another handler "makes settlement order implicit and
/// untestable", and injecting <c>ISender</c> is the same defect with an indirection in front of it.
/// </para>
/// <para>
/// <b>The cells are catalogue nodes, not grid tiles, and that remains the whole design.</b> The
/// spatial provider answers "which nodes are in this rectangle" and
/// <see cref="ILocationResourceCounter"/> has answered "how much of your vertical is in this node"
/// since B4. A grid would have needed a second, coordinate-shaped seam in seven modules — two of
/// which store no coordinate at all and could never have implemented it.
/// </para>
/// </summary>
public sealed class MapCellAggregator
{
    /// <summary>
    /// The most cells one response aggregates.
    /// <para>
    /// Chosen against cost rather than against a page size: the counters run in turn, so at the
    /// full set this is already well over a hundred sequential counts. A viewport with more places
    /// than this in it is a region, and a region's answer is the rung above.
    /// </para>
    /// </summary>
    public const int MaxCells = 20;

    private readonly GeospatialProviderRegistry _spatial;
    private readonly ResourceCountAggregator _counts;
    private readonly IOptions<LocationSpatialOptions> _options;

    public MapCellAggregator(
        GeospatialProviderRegistry spatial,
        ResourceCountAggregator counts,
        IOptions<LocationSpatialOptions> options)
    {
        _spatial = spatial;
        _counts = counts;
        _options = options;
    }

    /// <summary>The rung a caller may aggregate on, defaulting to the one the product is built around.</summary>
    public static AdministrativeLevel ParseLevel(string? value) =>
        Enum.TryParse<AdministrativeLevel>(value, ignoreCase: true, out var parsed)
        && parsed is AdministrativeLevel.Neighborhood or AdministrativeLevel.City
            ? parsed
            : AdministrativeLevel.Neighborhood;

    public async Task<Result<MapCellPage>> AggregateAsync(
        string bounds,
        string? level,
        IReadOnlyCollection<string>? resourceTypes,
        string? countryCode,
        CancellationToken cancellationToken = default)
    {
        if (!_spatial.Supports(GeospatialCapability.BoundingBoxSearch))
        {
            return Result.Failure<MapCellPage>(
                LocationErrors.SpatialCapabilityUnsupported(
                    "bounding-box search", _spatial.Active.Key));
        }

        var parsed = BoundingBox.TryParse(bounds);

        if (parsed.IsFailure)
        {
            return Result.Failure<MapCellPage>(parsed.Error);
        }

        var rung = ParseLevel(level);

        var country = string.IsNullOrWhiteSpace(countryCode)
            ? null
            : CountryCode.TryCreate(countryCode) is { IsSuccess: true } code
                ? code.Value
                : null;

        // One more than the cap, so "there were more" is a fact rather than an inference from a
        // full page — a viewport holding exactly MaxCells nodes is complete and must not claim
        // otherwise.
        var search = BoundingBoxSearch.Create(parsed.Value, [rung], country, MaxCells + 1);

        if (search.IsFailure)
        {
            return Result.Failure<MapCellPage>(search.Error);
        }

        var found = await _spatial.Active.FindWithinBoundsAsync(search.Value, cancellationToken);

        var truncated = found.Count > MaxCells;
        var nodes = truncated ? found.Take(MaxCells).ToList() : found;

        var requested = resourceTypes is { Count: > 0 }
            ? resourceTypes
                .Select(type => type.Trim())
                .Where(type => type.Length > 0)
                .ToList()
            : null;

        var minimum = Math.Max(1, _options.Value.MinimumClusterCellCount);

        var cells = new List<MapCell>(nodes.Count);
        var suppressed = 0;

        foreach (var node in nodes)
        {
            var area = node.Area;

            if (area.Center is null)
            {
                // A node with no centre cannot carry a marker. It was returned because the
                // provider matched something else about it; it is not a cell.
                continue;
            }

            var counts = await _counts.CountInAreaAsync(
                area.Id, area.Path, requested, cancellationToken);

            var total = counts.Values.Sum();

            if (total < minimum)
            {
                suppressed++;
                continue;
            }

            cells.Add(new MapCell(area, counts, total));
        }

        return Result.Success(new MapCellPage
        {
            Cells = [.. cells.OrderByDescending(cell => cell.Total)],
            Bounds = parsed.Value,
            Level = rung,
            ResourceTypes = requested ?? _counts.ResourceTypes,
            MinimumCellCount = minimum,
            SuppressedCells = suppressed,
            Truncated = truncated
        });
    }
}
