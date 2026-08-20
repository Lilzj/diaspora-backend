namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// The cross-vertical counters the country dashboard carries a band for.
/// <para>
/// <b>Names, not a schema.</b> These are the keys of an owned counter collection, stored as
/// strings, so a vertical that lands after B8.1 contributes a counter by publishing a contract
/// this module subscribes to — or by shipping an <c>ICountryContextContributor</c> — and needs no
/// migration here. The enum exists so the six B8.1 actually maintains are spelled one way in one
/// place rather than as string literals in seven event handlers.
/// </para>
/// </summary>
public enum CountryContextKind
{
    /// <summary>Published guides. The only counter this module owns outright.</summary>
    Guides = 0,

    Jobs = 1,

    Housing = 2,

    Marketplace = 3,

    Events = 4,

    Organizations = 5,

    Providers = 6
}
