using Diaspora.Modules.Providers.Domain.ValueObjects;

namespace Diaspora.Modules.Providers.Extensibility.Pricing;

/// <summary>
/// How a price becomes the string a client renders.
/// <para>
/// <b>The seam exists because currency formatting is a localisation problem, not a domain one,
/// and getting it wrong is visible on every card.</b> The client hard-codes
/// <c>£{min} — £{max}</c>, which is wrong for every member outside the UK and wrong for every
/// free service (it renders "£0 — £0"). B7 ships a formatter that is correct for the eleven
/// pricing models and every ISO currency, and a deployment that wants localised separators,
/// per-country conventions or a marketing voice registers its own.
/// </para>
/// <para>
/// <b>Formatted server-side rather than left to the client</b>, deliberately. Three clients
/// formatting the same price three ways is the fragmentation this platform already has in its
/// media URLs, and a price shown differently in a list and on a detail screen reads as a bug to
/// the person being charged it.
/// </para>
/// </summary>
public interface IProviderPricingFormatter
{
    /// <summary>The module contributing.</summary>
    string Module { get; }

    /// <summary>Lower runs first, and the first non-null result wins. B7's default is 1000.</summary>
    int Order => 100;

    /// <summary>
    /// Renders a price, or returns null to hand on to the next formatter.
    /// <para>
    /// Null rather than a fallback string, so a formatter that only knows about West African
    /// currencies can contribute those and leave everything else alone.
    /// </para>
    /// </summary>
    string? Format(ProviderPricing pricing, string? languageCode = null);
}
