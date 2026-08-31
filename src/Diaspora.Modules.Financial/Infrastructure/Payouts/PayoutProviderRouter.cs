using System.Globalization;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;

namespace Diaspora.Modules.Financial.Infrastructure.Payouts;

/// <summary>
/// Decides which provider may carry a payout, or says precisely why none may.
/// <para>
/// <b>This is a first-class component rather than a convenience, because provider availability is
/// not a property of a provider.</b> It is a property of a provider, a country, a rail, a currency,
/// an amount and the platform's own jurisdiction all at once — and the one arrangement that is
/// certainly wrong is a payment path that reaches for whichever adapter is registered. A platform
/// whose settlement rails differ by country cannot express that in a preference list, which is what
/// <c>PaymentGatewayRegistry</c> offers and why payouts do not use it.
/// </para>
/// <para>
/// <b>It never falls back, and that is the difference between money and messages.</b> The SMS router
/// this borrows its shape from returns a second wire to try, because re-sending a text that may have
/// arrived costs a fraction of a penny. A payout that timed out may already have moved the money;
/// re-routing on the strength of a failure is how a platform pays somebody twice. Alternates come
/// back on the result and are documented as usable only before anything has been submitted — after
/// that the next step is reconciliation against the provider that took the instruction.
/// </para>
/// <para>
/// <b>Refusals narrow from the general to the specific, so the reason is the one worth acting on.</b>
/// "Your country is not supported" and "your bank is not supported, try mobile money" lead to
/// different behaviour from the person reading them, and a router that returned null for both would
/// make the API choose a message it has no basis to choose.
/// </para>
/// <para>
/// <b>Where two sources describe the same route, the more cautious row wins.</b> Two sources
/// disagreeing about whether money can reach a country is a configuration conflict, and the only
/// resolution direction that cannot lose money is the one that declines. An operator who means to
/// enable a route edits the source that says otherwise rather than outvoting it.
/// </para>
/// </summary>
public sealed class PayoutProviderRouter
{
    private readonly IReadOnlyList<IPayoutCapabilitySource> _sources;
    private readonly HashSet<string> _registeredProviders;

    public PayoutProviderRouter(
        IEnumerable<IPayoutCapabilitySource> sources,
        IEnumerable<ISettlementProvider> settlementProviders)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(settlementProviders);

        _sources = [.. sources];
        _registeredProviders = settlementProviders
            .Select(provider => provider.Provider)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The route for a request, or the reason there is none. Never throws for an unsupported
    /// route — "nowhere to send this" is an answer, not a fault.
    /// </summary>
    public PayoutRoutingResult Resolve(PayoutRouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var country = NormalizeCountry(request.Country);

        if (country is null)
        {
            return PayoutRoutingResult.Refused(
                PayoutRefusalReason.CountryUnsupported,
                $"'{request.Country}' is not an ISO 3166-1 alpha-2 country code.");
        }

        var matrix = Collapse(_sources.SelectMany(source => source.GetCapabilities()));

        // An Unsupported row is a documented "this provider does not do this". It is carried in the
        // matrix so an operator can see that somebody checked, and it is dropped here because it is
        // never a candidate — leaving it in would make every later count wrong.
        var candidates = matrix
            .Where(capability => capability.Support != CapabilitySupport.Unsupported)
            .ToList();

        var byCountry = candidates
            .Where(capability => Matches(capability.Country, country))
            .ToList();

        if (byCountry.Count == 0)
        {
            return PayoutRoutingResult.Refused(PayoutRefusalReason.CountryUnsupported);
        }

        // A destination bound to a provider may only go to that provider: the token it holds was
        // issued by them and means nothing to anybody else. Applied after the country narrowing so
        // the refusal can tell "we do not reach your country" from "we no longer reach it the way
        // your saved destination does", which the member fixes differently.
        if (!string.IsNullOrWhiteSpace(request.RequiredProvider))
        {
            var bound = byCountry
                .Where(capability => Matches(capability.Provider, request.RequiredProvider.Trim()))
                .ToList();

            if (bound.Count == 0)
            {
                return PayoutRoutingResult.Refused(
                    PayoutRefusalReason.DestinationProviderUnavailable, request.RequiredProvider);
            }

            byCountry = bound;
        }

        var byMethod = byCountry
            .Where(capability => capability.Method == request.Method)
            .ToList();

        if (byMethod.Count == 0)
        {
            return PayoutRoutingResult.Refused(PayoutRefusalReason.MethodUnsupported);
        }

        var byCurrency = byMethod
            .Where(capability => Matches(capability.Currency, request.Amount.Currency))
            .ToList();

        if (byCurrency.Count == 0)
        {
            return PayoutRoutingResult.Refused(PayoutRefusalReason.CurrencyUnsupported);
        }

        var routable = byCurrency.Where(capability => capability.IsRoutable).ToList();

        if (routable.Count == 0)
        {
            // A supported route somebody switched off is a more actionable fact than an unconfirmed
            // one, so it is reported first when both are present.
            var disabled = byCurrency.FirstOrDefault(
                capability => capability.Support == CapabilitySupport.Supported);

            return disabled is not null
                ? PayoutRoutingResult.Refused(PayoutRefusalReason.RouteDisabled, disabled.Note)
                : PayoutRoutingResult.Refused(
                    PayoutRefusalReason.CapabilityUnknown,
                    byCurrency.Select(capability => capability.Note)
                        .FirstOrDefault(note => !string.IsNullOrWhiteSpace(note)));
        }

        // Registration is checked before the per-request rules. A matrix naming an adapter nobody
        // registered is the platform's own misconfiguration, and reporting it as an amount or
        // verification problem would send an operator looking in the wrong place.
        var registered = routable
            .Where(capability => _registeredProviders.Contains(capability.Provider))
            .ToList();

        if (registered.Count == 0)
        {
            return PayoutRoutingResult.Refused(
                PayoutRefusalReason.ProviderNotRegistered,
                string.Join(", ", routable.Select(capability => capability.Provider).Distinct()));
        }

        // Verification before amount, deliberately. Being unverified blocks every attempt until the
        // member does something about it; an amount outside the limits blocks only this one. Telling
        // them the transient thing first would have them fix it and hit the standing one anyway.
        var permitted = registered
            .Where(capability => request.PayeeVerified || !capability.RequiresVerifiedPayee)
            .ToList();

        if (permitted.Count == 0)
        {
            return PayoutRoutingResult.Refused(PayoutRefusalReason.PayeeVerificationRequired);
        }

        var affordable = permitted
            .Where(capability => capability.Accepts(request.Amount))
            .ToList();

        if (affordable.Count == 0)
        {
            return PayoutRoutingResult.Refused(
                PayoutRefusalReason.AmountOutOfRange, DescribeLimits(permitted));
        }

        // Priority first, then provider name. The tie-break is not cosmetic: without it the chosen
        // provider would depend on the order sources happened to be registered in, which is a
        // routing decision that changes when somebody reorders a DI block.
        var ordered = affordable
            .OrderBy(capability => capability.Priority)
            .ThenBy(capability => capability.Provider, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return PayoutRoutingResult.Routed(ordered[0], [.. ordered.Skip(1)]);
    }

    /// <summary>
    /// Every country this deployment can pay into over any rail, for the capability surface. Sorted,
    /// so a client rendering it does not have to.
    /// </summary>
    public IReadOnlyList<string> SupportedCountries() =>
        [.. Collapse(_sources.SelectMany(source => source.GetCapabilities()))
            .Where(capability => capability.IsRoutable
                && _registeredProviders.Contains(capability.Provider))
            .Select(capability => capability.Country)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(country => country, StringComparer.Ordinal)];

    /// <summary>
    /// Collapses rows describing the same route down to the most cautious one. See the type summary
    /// for why that direction.
    /// </summary>
    private static List<PayoutCapability> Collapse(IEnumerable<PayoutCapability> rows) =>
    [
        .. rows
            .Where(row => row is not null)
            .GroupBy(row => (
                Provider: row.Provider?.Trim() ?? string.Empty,
                Country: NormalizeCountry(row.Country) ?? string.Empty,
                row.Method,
                Currency: row.Currency?.Trim().ToUpperInvariant() ?? string.Empty),
                TupleComparer)
            .Select(group => group.Aggregate(MoreCautious))
    ];

    private static readonly IEqualityComparer<(string Provider, string Country,
        Domain.Enums.PayoutDestinationKind Method, string Currency)> TupleComparer =
        new RouteKeyComparer();

    private static PayoutCapability MoreCautious(PayoutCapability left, PayoutCapability right) =>
        left with
        {
            // Unsupported is a stronger statement than Unknown, and both block routing.
            Support = left.Support == CapabilitySupport.Unsupported
                || right.Support == CapabilitySupport.Unsupported
                    ? CapabilitySupport.Unsupported
                    : left.Support == CapabilitySupport.Unknown
                        || right.Support == CapabilitySupport.Unknown
                        ? CapabilitySupport.Unknown
                        : CapabilitySupport.Supported,
            Enabled = left.Enabled && right.Enabled,
            RequiresVerifiedPayee = left.RequiresVerifiedPayee || right.RequiresVerifiedPayee,
            Priority = Math.Min(left.Priority, right.Priority),
            MinimumMinorUnits = Tighter(left.MinimumMinorUnits, right.MinimumMinorUnits, higher: true),
            MaximumMinorUnits = Tighter(left.MaximumMinorUnits, right.MaximumMinorUnits, higher: false),
            Note = string.IsNullOrWhiteSpace(left.Note) ? right.Note : left.Note
        };

    /// <summary>
    /// The more restrictive of two bounds. A null bound is "no limit", so it never tightens
    /// anything and loses to any stated value.
    /// </summary>
    private static long? Tighter(long? left, long? right, bool higher)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return higher ? Math.Max(left.Value, right.Value) : Math.Min(left.Value, right.Value);
    }

    private static string DescribeLimits(IReadOnlyList<PayoutCapability> capabilities)
    {
        var minimum = capabilities
            .Select(capability => capability.MinimumMinorUnits)
            .Where(value => value is not null)
            .Min();

        var maximum = capabilities
            .Select(capability => capability.MaximumMinorUnits)
            .Where(value => value is not null)
            .Max();

        return string.Create(
            CultureInfo.InvariantCulture,
            $"minimum={Describe(minimum)}, maximum={Describe(maximum)}");

        static string Describe(long? bound) =>
            bound?.ToString(CultureInfo.InvariantCulture) ?? "none";
    }

    private static bool Matches(string? left, string right) =>
        !string.IsNullOrWhiteSpace(left)
        && string.Equals(left.Trim(), right, StringComparison.OrdinalIgnoreCase);

    /// <summary>Upper-cases and validates an ISO 3166-1 alpha-2 code. Null when it is not one.</summary>
    private static string? NormalizeCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return null;
        }

        var trimmed = country.Trim().ToUpperInvariant();

        return trimmed.Length == 2 && trimmed.All(char.IsAsciiLetterUpper) ? trimmed : null;
    }

    private sealed class RouteKeyComparer
        : IEqualityComparer<(string Provider, string Country,
            Domain.Enums.PayoutDestinationKind Method, string Currency)>
    {
        public bool Equals(
            (string Provider, string Country, Domain.Enums.PayoutDestinationKind Method, string Currency) left,
            (string Provider, string Country, Domain.Enums.PayoutDestinationKind Method, string Currency) right) =>
            string.Equals(left.Provider, right.Provider, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Country, right.Country, StringComparison.OrdinalIgnoreCase)
            && left.Method == right.Method
            && string.Equals(left.Currency, right.Currency, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(
            (string Provider, string Country, Domain.Enums.PayoutDestinationKind Method, string Currency) key) =>
            HashCode.Combine(
                key.Provider.ToUpperInvariant(),
                key.Country.ToUpperInvariant(),
                key.Method,
                key.Currency.ToUpperInvariant());
    }
}
