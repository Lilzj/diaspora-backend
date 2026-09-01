using Microsoft.Extensions.Options;

namespace Diaspora.Sms;

/// <summary>
/// Which wire carries a given destination, and which one carries it if the first will not.
/// </summary>
/// <param name="Primary">The gateway the routing table chose. Never null when a route was resolved.</param>
/// <param name="Fallback">
/// The gateway to try if <paramref name="Primary"/> fails <em>transiently</em>. Null when
/// falling back is switched off, when no second gateway is configured, or when the second one
/// would be the same as the first.
/// </param>
public sealed record SmsRouting(ISmsGateway Primary, ISmsGateway? Fallback);

/// <summary>
/// Chooses a gateway from the destination number.
/// <para>
/// This is the whole reason two providers are worth configuring. A Nigerian number and a German
/// number cost very different amounts on the same wire, and the cheap wire does not reach
/// Germany at all — so the choice has to be made per message rather than per deployment.
/// </para>
/// <para>
/// <b>Matching is longest-prefix.</b> Calling codes are not prefix-free: <c>+1</c> covers the
/// United States, Canada and much of the Caribbean, and <c>+1876</c> is Jamaica. A first-match
/// table would make routing depend on the order somebody happened to type the entries in, which
/// is the kind of configuration bug that only shows up as one country's members not receiving
/// codes.
/// </para>
/// </summary>
public sealed class SmsGatewayRouter
{
    private readonly IReadOnlyList<ISmsGateway> _gateways;
    private readonly IOptionsMonitor<SmsOptions> _options;

    public SmsGatewayRouter(IEnumerable<ISmsGateway> gateways, IOptionsMonitor<SmsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(gateways);
        ArgumentNullException.ThrowIfNull(options);

        _gateways = [.. gateways];
        _options = options;
    }

    /// <summary>
    /// Whether any gateway is registered at all.
    /// </summary>
    /// <remarks>
    /// Deliberately weaker than <see cref="Resolve"/>, which answers for a specific destination.
    /// This is the question a caller asks before it has a number in hand — "can this deployment
    /// send an SMS to anyone" — and the only honest answer without one.
    /// </remarks>
    public bool HasAnyGateway => _gateways.Count > 0;

    /// <summary>
    /// The route for a destination, or null when this deployment has no gateway that can carry it.
    /// <para>
    /// Null rather than an exception: "no SMS provider is configured" is a legitimate state for a
    /// development host, and the caller turns it into a delivery failure that the member's screen
    /// can explain. An exception here would surface as a 500 on a sign-in.
    /// </para>
    /// </summary>
    public SmsRouting? Resolve(string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        var options = _options.CurrentValue;
        var normalised = Normalise(destination);

        var routed = MatchRoute(options, normalised);
        var fallbackName = options.DefaultProvider;

        // A route naming a provider this deployment did not configure is a misconfiguration, and
        // the useful behaviour is to carry the message on the default wire rather than to drop
        // it. The gateway's own `IsConfigured` is what decides — not whether a section exists,
        // which can be present and empty.
        var primary = Find(routed) ?? Find(fallbackName);

        if (primary is null)
        {
            return null;
        }

        if (!options.FallBackToDefaultProvider)
        {
            return new SmsRouting(primary, null);
        }

        var fallback = Find(fallbackName);

        // Falling back to the gateway that just failed is two attempts at the same wire wearing
        // different names, and it doubles the cost of an outage without improving the odds.
        if (fallback is not null && ReferenceEquals(fallback, primary))
        {
            fallback = null;
        }

        return new SmsRouting(primary, fallback);
    }

    /// <summary>The provider named by the longest matching prefix, or null.</summary>
    private static string? MatchRoute(SmsOptions options, string destination)
    {
        string? winner = null;
        var winningLength = -1;

        foreach (var route in options.Routes)
        {
            if (string.IsNullOrWhiteSpace(route.Provider))
            {
                continue;
            }

            foreach (var code in route.CallingCodes)
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                var prefix = Normalise(code);

                if (prefix.Length > winningLength
                    && destination.StartsWith(prefix, StringComparison.Ordinal))
                {
                    winner = route.Provider;
                    winningLength = prefix.Length;
                }
            }
        }

        return winner;
    }

    private ISmsGateway? Find(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        foreach (var gateway in _gateways)
        {
            if (string.Equals(gateway.Provider, provider, StringComparison.OrdinalIgnoreCase)
                && gateway.IsConfigured)
            {
                return gateway;
            }
        }

        return null;
    }

    /// <summary>
    /// Digits only.
    /// <para>
    /// Both sides of the comparison go through this, so a route written <c>+234</c> matches a
    /// number stored <c>234...</c> and a route written <c>00234</c> does not silently fail to.
    /// Spaces and dashes are stripped for the same reason: a routing table is edited by hand.
    /// </para>
    /// </summary>
    private static string Normalise(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;

        foreach (var character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                buffer[length++] = character;
            }
        }

        return new string(buffer[..length]);
    }
}
