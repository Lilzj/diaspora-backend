using System.Globalization;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.ValueObjects;

namespace Diaspora.Modules.Providers.Extensibility.Pricing;

/// <summary>
/// The price formatting every deployment gets without configuring anything.
/// <para>
/// Correct for all eleven pricing models, which the client's single <c>£{min} — £{max}</c> is
/// not: a free service renders "Free" rather than "£0 — £0", a quoted service renders "Price on
/// request" rather than nothing at all, and an hourly rate renders "£40/hr" rather than being
/// indistinguishable from a fixed price.
/// </para>
/// <para>
/// <b>Symbols are used only where the mapping is unambiguous.</b> "$" means at least a dozen
/// different currencies on a platform serving diaspora communities across the Americas, Africa
/// and the Pacific, and a Canadian member shown "$40" by a US provider has been told something
/// false. Anything not on the short unambiguous list renders as its ISO code — "MUR 400",
/// "NGN 15,000" — which is longer and never wrong.
/// </para>
/// <para>
/// Runs last, at order 1000, so any contributed formatter takes precedence.
/// </para>
/// </summary>
public sealed class DefaultPricingFormatter : IProviderPricingFormatter
{
    /// <summary>
    /// The only symbols used. Each one maps to exactly one currency in ordinary usage; "$",
    /// "kr" and "R" are deliberately absent because they do not.
    /// </summary>
    private static readonly Dictionary<string, string> Symbols =
        new(StringComparer.Ordinal)
        {
            ["GBP"] = "£",
            ["EUR"] = "€",
            ["JPY"] = "¥",
            ["NGN"] = "₦",
            ["GHS"] = "₵",
            ["KES"] = "KSh",
            ["INR"] = "₹",
            ["ZAR"] = "R"
        };

    public string Module => ProvidersModuleMetadata.Name;

    public int Order => 1_000;

    public string? Format(ProviderPricing pricing, string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        return pricing.Model switch
        {
            PricingModel.Unspecified => null,
            PricingModel.Free => "Free",
            PricingModel.Donation => "Donation",
            PricingModel.Quote => "Price on request",
            PricingModel.Range => FormatRange(pricing),
            _ => FormatSingle(pricing)
        };
    }

    private static string? FormatRange(ProviderPricing pricing)
    {
        if (pricing.MinAmount is not { } min || pricing.MaxAmount is not { } max)
        {
            return null;
        }

        // An "up to" band that starts at zero reads better than "£0 – £15", and a band whose
        // ends are equal is a fixed price the caller happened to express as a range.
        if (min == max)
        {
            return Money(min, pricing.Currency);
        }

        return min == 0m
            ? $"Up to {Money(max, pricing.Currency)}"
            : $"{Money(min, pricing.Currency)} – {Money(max, pricing.Currency)}";
    }

    private static string? FormatSingle(ProviderPricing pricing)
    {
        if (pricing.MinAmount is not { } amount)
        {
            return null;
        }

        var money = Money(amount, pricing.Currency);

        var suffix = pricing.Model switch
        {
            PricingModel.Hourly => "/hr",
            PricingModel.Daily => "/day",
            PricingModel.PerSession => " per session",
            PricingModel.PerPerson => " per person",
            PricingModel.PerUnit when !string.IsNullOrWhiteSpace(pricing.Unit) => $"/{pricing.Unit}",
            PricingModel.Subscription when !string.IsNullOrWhiteSpace(pricing.Unit) => $"/{pricing.Unit}",
            PricingModel.Subscription => "/month",
            _ => string.Empty
        };

        // "From £40" is the honest rendering of a negotiable price, and it is what a provider
        // means when they tick the box.
        var prefix = pricing.IsNegotiable ? "From " : string.Empty;

        return $"{prefix}{money}{suffix}";
    }

    private static string Money(decimal amount, string? currency)
    {
        // Whole amounts lose their ".00" — a menu of "£8.00" reads like a receipt, not a price
        // list — while anything with pence keeps both decimal places.
        var rendered = amount == decimal.Truncate(amount)
            ? amount.ToString("N0", CultureInfo.InvariantCulture)
            : amount.ToString("N2", CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(currency))
        {
            return rendered;
        }

        return Symbols.TryGetValue(currency, out var symbol)
            ? $"{symbol}{rendered}"
            : $"{currency} {rendered}";
    }
}
