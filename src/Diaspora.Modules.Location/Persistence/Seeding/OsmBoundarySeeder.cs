using System.Text.Json;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Location.Persistence.Seeding;

/// <summary>
/// Gives the catalogue's towns, districts and regions the outlines OpenStreetMap publishes for them.
/// <para>
/// <b>Exact matches only, and that is most of the design.</b> The bundled file holds the
/// administrative boundaries whose OSM name and level are the catalogue's own — Greater London,
/// Manchester, Plaines Wilhems, Quatre Bornes and the rest. Salford is a city in OSM and a
/// neighbourhood here; "Beau Bassin-Rose Hill" is one municipality in OSM and two places here;
/// London's wards are electoral lines, not neighbourhoods. None of those is imported, because an
/// outline under the wrong node is a confident line in the wrong place, and members rent and
/// report on these lines. A node without an outline keeps its centre, which says less and claims
/// nothing.
/// </para>
/// <para>
/// <b>The licence travels with every ring.</b> The file is derived from OpenStreetMap and stays
/// under the Open Database License. Each outline is stored with its source, licence, credit and
/// OSM relation, so every surface that draws one can show "© OpenStreetMap contributors" and a
/// disputed line can be looked up where it was drawn.
/// </para>
/// <para>
/// Idempotent like every seeder: a node that has an outline keeps it, unless it is an older extract
/// of the same relation, which this file's newer one replaces.
/// </para>
/// </summary>
public sealed class OsmBoundarySeeder : ILocationCatalogSeeder
{
    /// <summary>The embedded file's name, as the project file declares it.</summary>
    public const string ResourceName = "Diaspora.Modules.Location.Seeding.osm-boundaries.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ILogger<OsmBoundarySeeder> _logger;

    public OsmBoundarySeeder(ILogger<OsmBoundarySeeder> logger)
    {
        _logger = logger;
    }

    /// <summary>After the launch geography, whose places these outline.</summary>
    public int Order => 200;

    public string Name => "osm-boundaries";

    public async Task SeedAsync(ILocationCatalogWriter writer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);

        foreach (var outline in ReadBundled())
        {
            var outcome = await writer.EnsureBoundaryAsync(
                outline.CountryCode,
                outline.Level,
                outline.Name,
                outline.Boundary,
                outline.Provenance,
                cancellationToken);

            if (outcome is BoundaryImportOutcome.Recorded or BoundaryImportOutcome.Refreshed)
            {
                _logger.LogInformation(
                    "Outline for {Level} '{Area}' ({Country}) {Outcome} from {Reference}.",
                    outline.Level, outline.Name.Value, outline.CountryCode.Value,
                    outcome, outline.Provenance.SourceReference);
            }
            else if (outcome is not BoundaryImportOutcome.AlreadyOutlined)
            {
                // Not an error: the catalogue is data and may not hold the place. It is worth a line,
                // because a missing outline on a launch town is visible on every map of it.
                _logger.LogWarning(
                    "Outline for {Level} '{Area}' ({Country}) not imported: {Outcome}.",
                    outline.Level, outline.Name.Value, outline.CountryCode.Value, outcome);
            }
        }
    }

    /// <summary>
    /// Every outline in the bundled file, parsed and validated.
    /// <para>
    /// Public so a test can prove the file the deployment ships is readable in full. An entry that
    /// fails validation is a packaging error, not a runtime condition, so it throws rather than
    /// being skipped where nobody would notice.
    /// </para>
    /// </summary>
    public static IReadOnlyList<ImportableBoundary> ReadBundled()
    {
        using var stream = typeof(OsmBoundarySeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The boundary file '{ResourceName}' is not embedded in the Location module.");

        var file = JsonSerializer.Deserialize<BoundaryFile>(stream, Json)
            ?? throw new InvalidOperationException($"The boundary file '{ResourceName}' is empty.");

        return file.Boundaries.Select(entry => Parse(file, entry)).ToList();
    }

    private static ImportableBoundary Parse(BoundaryFile file, BoundaryEntry entry)
    {
        string Describe() => $"'{entry.Name}' ({entry.CountryCode} {entry.Level}, relation {entry.OsmRelation})";

        var country = CountryCode.TryCreate(entry.CountryCode);
        var name = PlaceName.TryCreate(entry.Name);

        if (country.IsFailure || name.IsFailure
            || !Enum.TryParse<AdministrativeLevel>(entry.Level, ignoreCase: false, out var level))
        {
            throw new InvalidOperationException($"Boundary {Describe()} does not name a catalogue place.");
        }

        var vertices = new List<GeoCoordinate>(entry.Vertices.Count);

        foreach (var pair in entry.Vertices)
        {
            var vertex = pair.Count == 2 ? GeoCoordinate.TryCreate(pair[0], pair[1]) : null;

            if (vertex is null || vertex.IsFailure)
            {
                throw new InvalidOperationException($"Boundary {Describe()} has a vertex that is not a coordinate.");
            }

            vertices.Add(vertex.Value);
        }

        var boundary = GeographicBoundary.TryCreate(vertices);
        var provenance = BoundaryProvenance.TryCreate(
            file.Source, file.Licence, file.Attribution, $"relation/{entry.OsmRelation}", file.RetrievedOn);

        if (boundary.IsFailure)
        {
            throw new InvalidOperationException($"Boundary {Describe()}: {boundary.Error.Description}");
        }

        if (provenance.IsFailure)
        {
            throw new InvalidOperationException($"Boundary {Describe()}: {provenance.Error.Description}");
        }

        return new ImportableBoundary(country.Value, level, name.Value, boundary.Value, provenance.Value);
    }

    private sealed record BoundaryFile(
        string Source,
        string Licence,
        string Attribution,
        DateOnly RetrievedOn,
        IReadOnlyList<BoundaryEntry> Boundaries);

    private sealed record BoundaryEntry(
        string CountryCode,
        string Level,
        string Name,
        long OsmRelation,
        IReadOnlyList<IReadOnlyList<double>> Vertices);
}

/// <summary>One outline from the bundled file, ready for <see cref="ILocationCatalogWriter.EnsureBoundaryAsync"/>.</summary>
public sealed record ImportableBoundary(
    CountryCode CountryCode,
    AdministrativeLevel Level,
    PlaceName Name,
    GeographicBoundary Boundary,
    BoundaryProvenance Provenance);
