using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Infrastructure.Location;

/// <summary>
/// Contributes post counts to Location's area and country statistics.
/// <para>
/// The counts a neighbourhood hub renders as "12 updates this week" come from here. Counting is
/// a prefix match on the hierarchy path Location itself materialised and B6 copied — no
/// hierarchy reasoning happens in this module, only a string comparison against a value Location
/// computed.
/// </para>
/// </summary>
public sealed class CommunityResourceCounter : ILocationResourceCounter
{
    private readonly ICommunityPostRepository _posts;

    public CommunityResourceCounter(ICommunityPostRepository posts) => _posts = posts;

    public string ResourceType => CommunityNearbyResourceProvider.Key;

    public Task<int> CountInAreaAsync(
        AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        return _posts.CountInAreaAsync(path.Value, cancellationToken);
    }

    public Task<int> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        return _posts.CountInCountryAsync(countryCode.Value, cancellationToken);
    }
}
