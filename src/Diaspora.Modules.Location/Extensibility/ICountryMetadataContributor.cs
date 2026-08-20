using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// Implemented to supply country metadata from somewhere other than this module's own seed.
/// <para>
/// The seam exists because country metadata has a long tail nobody wants in this repository.
/// Currency and calling code are stable and shipped; landmark photography, supported-market
/// status and payment-corridor availability are not, and each belongs to whichever capability
/// actually owns it. A payments module knowing which currencies it can settle should be able
/// to say so without this module acquiring a dependency on payments.
/// </para>
/// <para>
/// Contributors are applied in <see cref="Order"/> and later ones win, so a deployment-specific
/// override can sit above the shipped defaults without editing them.
/// </para>
/// </summary>
public interface ICountryMetadataContributor
{
    /// <summary>Lower runs first; later contributors override earlier ones.</summary>
    int Order => 0;

    /// <summary>
    /// The metadata this contributor knows for the country, or null when it knows none.
    /// <para>
    /// Returning null rather than an empty record matters: an empty record overrides, and a
    /// contributor that meant "no opinion" would erase the values beneath it.
    /// </para>
    /// </summary>
    CountryMetadata? MetadataFor(CountryCode countryCode);
}
