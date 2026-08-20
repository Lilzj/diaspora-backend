using System.ComponentModel.DataAnnotations;
using Diaspora.Platform.Operations.Scalability;
using Microsoft.Extensions.Options;

namespace Diaspora.Operations.Scalability;

/// <summary>How this deployment is laid out.</summary>
public sealed class TopologyOptions
{
    public const string SectionName = "Topology";

    /// <summary>The region this instance is in.</summary>
    [Required]
    public string Region { get; set; } = "default";

    /// <summary>The availability zone, when the platform is told.</summary>
    public string? Zone { get; set; }

    /// <summary>Whether this region takes writes.</summary>
    public bool IsPrimary { get; set; } = true;

    /// <summary>
    /// Read replica connection strings.
    /// <para>
    /// Declared but not yet consulted: the shipped router sends every read to the writer. Present so a
    /// deployment can configure them, and so the dashboard can report that they exist while the router
    /// is not yet using them — which is more useful than pretending the setting does not exist.
    /// </para>
    /// </summary>
    public IList<string> ReadReplicas { get; } = [];

    /// <summary>Other regions, if the deployment knows about any.</summary>
    public IList<RegionConfiguration> Regions { get; } = [];

    /// <summary>Base URL for a content delivery network. Empty serves from the origin.</summary>
    public string? ContentDeliveryBaseUrl { get; set; }
}

/// <summary>One configured region.</summary>
public sealed class RegionConfiguration
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public RegionRole Role { get; set; } = RegionRole.Secondary;

    public ReplicationMode Replication { get; set; } = ReplicationMode.None;

    public IList<string> Zones { get; } = [];

    /// <summary>The countries this region serves.</summary>
    public IList<string> Countries { get; } = [];
}

/// <summary>
/// The shipped <see cref="IRegionContext"/>: one region, reported honestly.
/// <para>
/// The value is not in what it computes — it computes almost nothing — but in that the region is a
/// value the platform can read from now on. A telemetry attribute, a cache key prefix, a lock name and
/// a health report can all carry it today, which is where a multi-region migration otherwise spends
/// its time: retrofitting a region into a hundred places that never had one.
/// </para>
/// </summary>
public sealed class SingleRegionContext : IRegionContext
{
    private readonly TopologyOptions _options;

    public SingleRegionContext(IOptions<TopologyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;

        var configured = _options.Regions
            .Select(region => new RegionDescriptor(
                region.Name,
                region.Role,
                [.. region.Zones],
                [.. region.Countries],
                region.Replication))
            .ToList();

        if (!configured.Any(region =>
            string.Equals(region.Name, _options.Region, StringComparison.OrdinalIgnoreCase)))
        {
            // The instance's own region always appears, whether or not the deployment listed it. A
            // topology that does not contain the machine reading it is not a topology anyone can reason
            // about.
            configured.Insert(0, new RegionDescriptor(
                _options.Region,
                _options.IsPrimary ? RegionRole.Primary : RegionRole.Secondary,
                _options.Zone is null ? [] : [_options.Zone],
                [],
                ReplicationMode.None));
        }

        Regions = configured;
    }

    public string CurrentRegion => _options.Region;

    public string? CurrentZone => _options.Zone;

    public bool IsPrimary => _options.IsPrimary;

    public IReadOnlyList<RegionDescriptor> Regions { get; }

    public RegionDescriptor ResolveForCountry(string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        return Regions.FirstOrDefault(region =>
                region.Countries.Contains(countryCode, StringComparer.OrdinalIgnoreCase))
            ?? Regions.FirstOrDefault(region => region.Role == RegionRole.Primary)
            ?? Regions[0];
    }
}

/// <summary>
/// The shipped router: everything reads from the writer.
/// <para>
/// <b>Behaviour is unchanged from before B17, deliberately.</b> Routing reads to a replica without a
/// read-your-own-writes guarantee is worse than not routing them at all: a member who posts and is
/// immediately shown a feed built from a lagging replica sees their own post missing, concludes the
/// platform lost it, and posts again.
/// </para>
/// <para>
/// What ships is the shape of that guarantee. <see cref="MarkWritten"/> is called by the unit of work
/// on save, and <see cref="RequiresConsistency"/> stays true for the rest of the scope — so the day a
/// replica is configured, the correctness question has already been answered rather than being
/// discovered in production.
/// </para>
/// </summary>
public sealed class PrimaryOnlyReadReplicaRouter : IReadReplicaRouter
{
    private readonly TopologyOptions _options;
    private bool _written;

    public PrimaryOnlyReadReplicaRouter(IOptions<TopologyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public bool HasReplicas => _options.ReadReplicas.Count > 0;

    /// <summary>
    /// Always true while no replica is being used, and true after a write regardless. Two reasons,
    /// stated separately so that registering a real router only has to change one of them.
    /// </summary>
    public bool RequiresConsistency => true;

    public void MarkWritten() => _written = true;

    /// <summary>
    /// The writer, always.
    /// <para>
    /// The connection string is not resolved here — that is the host's, because this assembly does not
    /// reference the persistence layer. An empty string means "use the configured default", which is
    /// what the host does today.
    /// </para>
    /// </summary>
    public string ResolveReadConnection() => string.Empty;

    /// <summary>Whether this scope has written. Read by the dashboard; the router itself always says yes.</summary>
    public bool HasWritten => _written;
}
