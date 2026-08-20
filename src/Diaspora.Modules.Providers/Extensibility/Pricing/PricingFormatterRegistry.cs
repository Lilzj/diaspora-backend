using Diaspora.Modules.Providers.Domain.ValueObjects;

namespace Diaspora.Modules.Providers.Extensibility.Pricing;

/// <summary>
/// Runs the registered price formatters in order and takes the first that produces a string.
/// <para>
/// No isolation here, unlike the verification and availability registries, and the asymmetry is
/// intentional: formatting is pure and synchronous with no I/O, so a formatter that throws has a
/// bug rather than a dependency, and swallowing it would hide the bug behind a price that
/// silently renders differently on one deployment. Failing loudly in a test is the right cost.
/// </para>
/// </summary>
public sealed class PricingFormatterRegistry
{
    private readonly IReadOnlyList<IProviderPricingFormatter> _formatters;

    public PricingFormatterRegistry(IEnumerable<IProviderPricingFormatter> formatters)
    {
        ArgumentNullException.ThrowIfNull(formatters);

        _formatters = formatters.OrderBy(formatter => formatter.Order).ToList();
    }

    /// <summary>The display string for a price, or null when nothing should be shown.</summary>
    public string? Format(ProviderPricing pricing, string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        foreach (var formatter in _formatters)
        {
            var formatted = formatter.Format(pricing, languageCode);

            if (!string.IsNullOrWhiteSpace(formatted))
            {
                return formatted;
            }
        }

        return null;
    }
}
