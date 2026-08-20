using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Infrastructure.Location;

/// <summary>
/// Tells Location how many jobs are in a place.
/// <para>
/// <b>Jobs was the one vertical of seven with no counter, and nothing said so.</b> B4's registry
/// has answered "how much of your vertical is in this node" since B4 shipped, and Housing,
/// Marketplace, Providers, Guides, Community Updates and Community Life all register one. Jobs
/// registered a <c>INearbyResourceProvider</c>-shaped geographic scope adapter and never a counter,
/// so every neighbourhood hub on the platform has rendered a dash where a jobs figure belongs — and
/// because <c>ResourceCountAggregator</c>'s contract is that an unreachable vertical omits its key,
/// the absence was indistinguishable from a vertical that was merely slow. B21 found it building
/// the density layer, where a <c>jobs</c> layer had no source at all.
/// </para>
/// <para>
/// <b>Public postings only, and by path rather than by identifier.</b> The default
/// <see cref="JobQueryFilter"/> excludes drafts, private and hidden postings — no viewer is set, so
/// nothing non-public is in scope — and a path prefix means a city counts everything in its
/// neighbourhoods rather than only what was filed directly against the city itself. Both match what
/// the other six counters do, because a hub whose figures were each scoped differently would be
/// comparing things that are not comparable.
/// </para>
/// </summary>
public sealed class JobResourceCounter : ILocationResourceCounter
{
    /// <summary>The key B4 reserved for jobs. Not a new one.</summary>
    public const string Key = "jobs";

    private readonly IJobPostingRepository _postings;

    public JobResourceCounter(IJobPostingRepository postings) => _postings = postings;

    public string ResourceType => Key;

    public async Task<int> CountInAreaAsync(
        AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        return await _postings.CountAsync(
            new JobQueryFilter { AreaPathPrefix = path.Value, OpenOnly = true },
            cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        return await _postings.CountAsync(
            new JobQueryFilter { CountryCode = countryCode.Value, OpenOnly = true },
            cancellationToken);
    }
}
