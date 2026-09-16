using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;

namespace Diaspora.Modules.Jobs.Application.Services;

/// <summary>
/// Files a job posting under the catalogue node its place names.
/// <para>
/// <b>Until 16 Sep 2026 no posting was filed anywhere.</b> Create and update both built the place
/// from the request's country, region, city and area id and never asked Location for a path, so
/// the jobs counter — which aggregates by path — read zero for every town, and the map's cluster
/// endpoint could not see a single job. A posting carries no coordinates, so the names are the
/// evidence, and Location's <see cref="IAreaLocator"/> decides which node they mean.
/// </para>
/// </summary>
public sealed class JobAreaFiling
{
    private readonly IAreaLocator _locator;

    public JobAreaFiling(IAreaLocator locator) => _locator = locator;

    /// <summary>The place, filed. One that is already filed is returned as it is.</summary>
    public async Task<JobLocationContext> FileAsync(
        JobLocationContext location, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (!string.IsNullOrWhiteSpace(location.AreaPath))
        {
            return location;
        }

        var placement = await _locator.LocateAsync(
            new AreaFilingRequest
            {
                CountryCode = location.CountryCode,
                AreaId = location.AreaId,
                CityName = location.CityName,
                RegionName = location.RegionName
            },
            cancellationToken);

        return placement is null
            ? location
            : location.WithArea(placement.AreaId, placement.Path);
    }
}
