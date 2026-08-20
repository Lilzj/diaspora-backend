using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.Modules.Providers.Extensibility.Pricing;

namespace Diaspora.Modules.Providers.Application.Mapping;

/// <summary>
/// Turns a price into the shape the wire reads, including the string a client renders.
/// <para>
/// An instance rather than a static, because formatting goes through the registry and the
/// registry is scoped — a contributed formatter may hold a locale service or a currency-rate
/// client. Returning the pre-formatted string alongside the structure is what stops three
/// clients rendering the same price three ways.
/// </para>
/// </summary>
public sealed class PricingMapper
{
    private readonly PricingFormatterRegistry _formatters;

    public PricingMapper(PricingFormatterRegistry formatters) => _formatters = formatters;

    public PricingResponse ToResponse(ProviderPricing pricing, string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        return new PricingResponse
        {
            Model = ProviderWireNames.ToWire(pricing.Model),
            MinAmount = pricing.MinAmount,
            MaxAmount = pricing.MaxAmount,
            Currency = pricing.Currency,
            Unit = pricing.Unit,
            IsNegotiable = pricing.IsNegotiable,
            DisplayText = _formatters.Format(pricing, languageCode)
        };
    }
}
