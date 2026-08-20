using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// Resolves a country's metadata by asking every contributor in order and letting later ones
/// win.
/// <para>
/// Last-wins rather than first-wins so that overriding requires only registering a
/// higher-ordered contributor, never removing a lower one. The shipped defaults sit at order
/// zero and a deployment's own corrections sit above them; neither has to know about the
/// other.
/// </para>
/// </summary>
public sealed class CountryMetadataCatalog
{
    private readonly List<ICountryMetadataContributor> _contributors;

    public CountryMetadataCatalog(IEnumerable<ICountryMetadataContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        _contributors = contributors
            .OrderBy(contributor => contributor.Order)
            .ThenBy(contributor => contributor.GetType().FullName, StringComparer.Ordinal)
            .ToList();
    }

    public int ContributorCount => _contributors.Count;

    /// <summary>
    /// The resolved metadata, or <see cref="CountryMetadata.Empty"/> when nobody knows any.
    /// </summary>
    public CountryMetadata MetadataFor(CountryCode countryCode)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        var resolved = CountryMetadata.Empty;

        foreach (var contributor in _contributors)
        {
            var contributed = contributor.MetadataFor(countryCode);

            if (contributed is not null)
            {
                resolved = contributed;
            }
        }

        return resolved.WithDerivedLocale(countryCode);
    }
}
