using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Extensibility.Payouts;

/// <summary>
/// What is being asked of the router: pay this much, in this currency, to this kind of destination,
/// for somebody in this country.
/// <para>
/// <b>Every field is a fact the platform established, never one a client sent.</b> The country comes
/// off the withdrawal, the amount off the held ledger entry, the verification state off the payee's
/// identity record. A request shape that could be populated from a request body would be a routing
/// decision an attacker could steer.
/// </para>
/// </summary>
public sealed record PayoutRouteRequest
{
    /// <summary>ISO 3166-1 alpha-2. Where the payee is, not where the platform is.</summary>
    public required string Country { get; init; }

    /// <summary>The rail the destination represents.</summary>
    public required PayoutDestinationKind Method { get; init; }

    /// <summary>
    /// What would leave, in the currency it is denominated in. The router never converts — a route
    /// in another currency is not a cheaper route, it is a different transaction.
    /// </summary>
    public required Money Amount { get; init; }

    /// <summary>
    /// Whether the payee has completed identity verification. Rows that require it are skipped when
    /// this is false, and the refusal says so rather than reporting the country as unsupported.
    /// </summary>
    public bool PayeeVerified { get; init; }

    /// <summary>
    /// The only provider that may carry this, when the destination is bound to one.
    /// <para>
    /// <b>A payout destination holds a token the provider that issued it can read and nobody else
    /// can.</b> Routing a destination registered with one provider to another would hand over an
    /// opaque string to an API that interprets it as one of its own — which fails, and fails after
    /// the money has been marked as leaving. Null means the destination is not bound and any capable
    /// provider may take it.
    /// </para>
    /// </summary>
    public string? RequiredProvider { get; init; }
}

/// <summary>
/// Why no provider was chosen.
/// <para>
/// <b>One reason per cause, because "unavailable" is not an answer anybody can act on.</b> A member
/// told their country is unsupported will stop trying; a member told their <em>bank</em> is
/// unsupported will try mobile money. An operator told a capability is
/// <see cref="CapabilitySupport.Unknown"/> knows to go and read a provider's documentation, which
/// is a different job from onboarding a new country.
/// </para>
/// </summary>
public enum PayoutRefusalReason
{
    /// <summary>A provider was chosen. Not a refusal.</summary>
    None = 0,

    /// <summary>Nothing at all is configured for that country.</summary>
    CountryUnsupported = 1,

    /// <summary>The country is served, but not over that rail.</summary>
    MethodUnsupported = 2,

    /// <summary>The country and rail are served, but not in that currency.</summary>
    CurrencyUnsupported = 3,

    /// <summary>
    /// Rows match, and every one of them is <see cref="CapabilitySupport.Unknown"/>. The platform
    /// declines rather than guessing, and the note on the row says what nobody confirmed.
    /// </summary>
    CapabilityUnknown = 4,

    /// <summary>Rows match and are supported, but this deployment has switched them off.</summary>
    RouteDisabled = 5,

    /// <summary>A route exists, and the amount is outside every matching row's limits.</summary>
    AmountOutOfRange = 6,

    /// <summary>A route exists and requires a verified payee. This one is not.</summary>
    PayeeVerificationRequired = 7,

    /// <summary>
    /// The matrix names a provider that no adapter answers to. A misconfiguration rather than a
    /// capability gap, and it is reported separately so it does not read to an operator as a
    /// country problem.
    /// </summary>
    ProviderNotRegistered = 8,

    /// <summary>
    /// Every route that could have carried this needs the payee onboarded with that provider, and
    /// none of them is. Distinct from <see cref="PayeeVerificationRequired"/>, which is the
    /// platform's own identity check — this one is a provider's, and the member fixes it by
    /// finishing that provider's onboarding rather than the platform's.
    /// </summary>
    ProviderOnboardingRequired = 9,

    /// <summary>
    /// The destination is bound to a provider that cannot serve this corridor. Distinct from
    /// <see cref="CountryUnsupported"/> because the platform may well reach that country by another
    /// rail — the member fixes it by registering a new destination, not by waiting for coverage.
    /// </summary>
    DestinationProviderUnavailable = 10
}

/// <summary>
/// The router's answer: one provider, or one reason there is none.
/// <para>
/// <b><see cref="Alternates"/> is not a failover list, and the distinction is a financial one.</b>
/// The SMS router this borrows its shape from hands back a fallback wire because sending the same
/// text twice costs a fraction of a penny and annoys nobody. A payout is not that: a provider that
/// timed out may still have moved the money, and re-routing on the strength of a failure is how a
/// platform pays somebody twice. Alternates are the routes that <em>would also have served</em>,
/// usable only while nothing has been submitted — once an instruction has left, the next step is
/// reconciliation against the provider that took it, never another provider.
/// </para>
/// </summary>
public sealed record PayoutRoutingResult
{
    private PayoutRoutingResult() { }

    /// <summary>The chosen row. Null when <see cref="Refusal"/> says why not.</summary>
    public PayoutCapability? Capability { get; private init; }

    /// <summary>Routes that also matched, best first. Never a retry target — see the type summary.</summary>
    public IReadOnlyList<PayoutCapability> Alternates { get; private init; } = [];

    /// <summary><see cref="PayoutRefusalReason.None"/> when a provider was chosen.</summary>
    public PayoutRefusalReason Refusal { get; private init; }

    /// <summary>
    /// The note from the rows that came closest, when there is one. Carries the open question for a
    /// <see cref="PayoutRefusalReason.CapabilityUnknown"/> refusal so an operator does not have to
    /// go and find the matrix to learn what is missing.
    /// </summary>
    public string? Detail { get; private init; }

    public bool IsRouted => Capability is not null;

    /// <summary>The provider that was chosen. Throws when nothing was — check <see cref="IsRouted"/>.</summary>
    public string Provider => Capability?.Provider
        ?? throw new InvalidOperationException(
            $"No payout route was found ({Refusal}). Check IsRouted before reading Provider.");

    public static PayoutRoutingResult Routed(
        PayoutCapability capability, IReadOnlyList<PayoutCapability>? alternates = null)
    {
        ArgumentNullException.ThrowIfNull(capability);

        return new PayoutRoutingResult
        {
            Capability = capability,
            Alternates = alternates ?? [],
            Refusal = PayoutRefusalReason.None
        };
    }

    public static PayoutRoutingResult Refused(PayoutRefusalReason reason, string? detail = null)
    {
        if (reason == PayoutRefusalReason.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason), "A refusal must say what it is refusing for.");
        }

        return new PayoutRoutingResult { Refusal = reason, Detail = detail };
    }
}
