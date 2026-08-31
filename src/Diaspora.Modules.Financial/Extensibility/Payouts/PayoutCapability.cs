using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Extensibility.Payouts;

/// <summary>
/// Whether a provider can be used for something, as far as this deployment actually knows.
/// <para>
/// <b>Three values rather than two, and the third is the point.</b> A capability matrix built from
/// provider documentation has gaps in it — a country a provider lists without saying which payout
/// methods reach it, a currency that appears in a pricing page and in no API reference. The
/// tempting move is to record such a row as supported and find out in production; the honest one is
/// to record that nobody checked. <see cref="Unknown"/> is never routed to and is reported
/// separately from <see cref="Unsupported"/>, so an operator can tell "this provider cannot do it"
/// from "we have not confirmed whether it can".
/// </para>
/// </summary>
public enum CapabilitySupport
{
    /// <summary>
    /// Nobody has confirmed this against provider documentation. Never routed to. The default, so
    /// that a row somebody forgot to fill in fails closed rather than open.
    /// </summary>
    Unknown = 0,

    /// <summary>Confirmed against the provider's own documentation or account configuration.</summary>
    Supported = 1,

    /// <summary>Confirmed as not offered. Recorded rather than omitted — see the type summary.</summary>
    Unsupported = 2
}

/// <summary>
/// One row of the payout capability matrix: this provider, in this country, to this kind of
/// destination, in this currency.
/// <para>
/// <b>Every row is explicit and there are no wildcards.</b> A row meaning "all currencies" would
/// have to be resolved against a list of all currencies that this module does not have and should
/// not invent, and the first time it mattered would be a payout attempted in a currency the
/// provider settles in nowhere. Verbosity in configuration is the correct trade against a routing
/// table that quietly claims more than anybody verified.
/// </para>
/// <para>
/// <b>The row carries its own limits rather than deferring to a global one.</b> Provider payout
/// minimums differ by country and rail — a mobile-money transfer and a bank wire in the same
/// country do not share a floor — and a single platform-wide minimum would be wrong in both
/// directions at once.
/// </para>
/// <para>
/// <b>Nothing here names a vendor in a type.</b> <see cref="Provider"/> is a string an adapter
/// answers to, matching <c>IPaymentGateway.Provider</c> and <c>ISettlementProvider.Provider</c>;
/// the architecture test that forbids vendor names in this assembly is why, and the string is
/// stored and compared and never interpreted.
/// </para>
/// </summary>
public sealed record PayoutCapability
{
    /// <summary>The adapter this row describes. Matched against <c>ISettlementProvider.Provider</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case. Where the person being paid is.</summary>
    public required string Country { get; init; }

    /// <summary>The rail money would arrive on.</summary>
    public required PayoutDestinationKind Method { get; init; }

    /// <summary>ISO 4217, upper case. What the provider settles this route in.</summary>
    public required string Currency { get; init; }

    /// <summary>Defaults to <see cref="CapabilitySupport.Unknown"/> — see <see cref="CapabilitySupport"/>.</summary>
    public CapabilitySupport Support { get; init; } = CapabilitySupport.Unknown;

    /// <summary>
    /// Whether the deployment has switched this route on. Separate from <see cref="Support"/>:
    /// a provider may be perfectly capable of a route that this deployment has not finished
    /// onboarding for, and conflating the two would lose the distinction between "cannot" and
    /// "not yet".
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Lower is preferred. Ties break on provider name, so routing never depends on load order.</summary>
    public int Priority { get; init; }

    /// <summary>The smallest payout this route accepts, in minor units. Null means no floor.</summary>
    public long? MinimumMinorUnits { get; init; }

    /// <summary>The largest, in minor units. Null means no ceiling this module knows of.</summary>
    public long? MaximumMinorUnits { get; init; }

    /// <summary>
    /// Whether the provider requires the payee to have completed identity verification before it
    /// will move money to them. Declared per row because it is a provider-and-country fact rather
    /// than a platform policy.
    /// </summary>
    public bool RequiresVerifiedPayee { get; init; }

    /// <summary>
    /// Whether the provider requires the payee to have completed <em>its own</em> onboarding — a
    /// connected account, a merchant record — before it will pay them. Separate from
    /// <see cref="RequiresVerifiedPayee"/>, which is the platform's own identity check: a member
    /// Trust has fully verified may still have done nothing with a provider they have never been
    /// routed to. Answered by <see cref="IPayoutProviderVerificationSource"/>, and refused when
    /// nothing can answer.
    /// </summary>
    public bool RequiresProviderOnboarding { get; init; }

    /// <summary>
    /// Why this row says what it says. Carries the documentation reference for a
    /// <see cref="CapabilitySupport.Supported"/> row and the open question for an
    /// <see cref="CapabilitySupport.Unknown"/> one, so the gap is greppable rather than folklore.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>
    /// Whether this row could carry the given amount. Currency is not checked here — the router
    /// has already matched it, and a limit in a different currency would be meaningless.
    /// </summary>
    public bool Accepts(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (MinimumMinorUnits is { } floor && amount.MinorUnits < floor)
        {
            return false;
        }

        return MaximumMinorUnits is not { } ceiling || amount.MinorUnits <= ceiling;
    }

    /// <summary>
    /// Whether the router may select this row at all, before any per-request check. An
    /// <see cref="CapabilitySupport.Unknown"/> row is never routable — that is the whole reason
    /// the value exists.
    /// </summary>
    public bool IsRoutable => Enabled && Support == CapabilitySupport.Supported;
}
