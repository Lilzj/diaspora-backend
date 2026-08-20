using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Modules.Financial.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Gateways;

/// <summary>
/// Picks an adapter by what it says it supports and what the deployment prefers.
/// <para>
/// <b>Routing is configuration, not a switch statement.</b> A deployment operating in three
/// countries with three providers orders them in <c>Financial:GatewayPreference</c>; the alternative
/// — a chain of country comparisons in a handler — is the shape that makes adding a fourth provider a
/// code change in the payment path.
/// </para>
/// <para>
/// An adapter declaring no supported currencies is taken to support all of them, because the
/// alternative is every adapter having to enumerate the world.
/// </para>
/// </summary>
public sealed class PaymentGatewayRegistry : IPaymentGatewayRegistry
{
    private readonly IReadOnlyList<IPaymentGateway> _gateways;
    private readonly IReadOnlyList<string> _preference;

    public PaymentGatewayRegistry(
        IEnumerable<IPaymentGateway> gateways, IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _gateways = [.. gateways];
        _preference = [.. options.Value.GatewayPreference];
    }

    public IReadOnlyList<IPaymentGateway> All => _gateways;

    public IPaymentGateway? Find(string provider) =>
        string.IsNullOrWhiteSpace(provider)
            ? null
            : _gateways.FirstOrDefault(gateway =>
                string.Equals(gateway.Provider, provider.Trim(), StringComparison.OrdinalIgnoreCase));

    public IPaymentGateway? Resolve(PaymentMethodKind methodKind, string currency, string? countryCode)
    {
        var normalized = currency?.Trim().ToUpperInvariant() ?? string.Empty;

        var candidates = _gateways
            .Where(gateway => gateway.SupportedMethods.Contains(methodKind))
            .Where(gateway =>
                gateway.SupportedCurrencies.Count == 0
                || gateway.SupportedCurrencies.Contains(normalized))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        // Preference order first; anything unlisted follows in registration order, so adding an
        // adapter without touching configuration still works.
        return candidates
            .OrderBy(gateway => IndexOf(gateway.Provider))
            .First();
    }

    private int IndexOf(string provider)
    {
        for (var index = 0; index < _preference.Count; index++)
        {
            if (string.Equals(_preference[index], provider, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
