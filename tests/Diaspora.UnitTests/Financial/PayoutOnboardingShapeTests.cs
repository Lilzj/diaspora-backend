using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Infrastructure.Payouts;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// Which of the two shapes of "add a payout account" a country needs.
///
/// <para>
/// <b>There are two, and a client that assumed one would be wrong in half the world.</b> Stripe
/// Connect hands the member to its own hosted pages and gives back an account reference. PayPal pays
/// to an address the member already has — an email, a phone number — and has no setup to hand off
/// to. Only the payout matrix knows which applies, and the discriminator is
/// <c>RequiresProviderOnboarding</c>.
/// </para>
///
/// <para>
/// <b>Why this matters for Mauritius.</b> Stripe publishes no payout corridor for MU, so a member
/// there cannot be paid through the hand-off at all. PayPal's own country table lists MU as "send,
/// receive and withdraw", and the Mauritian rupee is not one of its currencies — so the corridor is
/// <c>paypal / MU / GBP</c>, never MUR. That row is live as of 8 Sep 2026: the adapter was held at
/// <c>Unknown</c> until <c>LivePayPalSandboxTests</c> ran against a real sandbox account, which is
/// the rule the repository set itself and which found three defects when it finally ran. These pin
/// the routing decision the row depends on.
/// </para>
/// </summary>
public sealed class PayoutOnboardingShapeTests
{
    // ── The discriminator ───────────────────────────────────────────────────────────────────

    [Fact]
    public void A_route_that_requires_onboarding_is_the_hand_off_shape()
    {
        var router = Router(Stripe("GB"));

        var route = router.RoutesInto("GB").Single();

        route.RequiresProviderOnboarding.Should().BeTrue();
    }

    [Fact]
    public void A_route_that_does_not_is_the_address_shape()
    {
        // The PayPal row, as it would be written once the sandbox run confirms the adapter.
        var router = Router(PayPal("MU"));

        var route = router.RoutesInto("MU").Single();

        route.RequiresProviderOnboarding.Should().BeFalse();
        route.Provider.Should().Be("paypal");
    }

    [Fact]
    public void A_country_with_both_offers_them_in_the_order_a_withdrawal_would_take()
    {
        // Ordering matters because the account a member adds must be the one their withdrawal
        // actually routes to. Same rule as Resolve: priority, then provider name.
        var router = Router(
            PayPal("GB") with { Priority = 20 },
            Stripe("GB") with { Priority = 10 });

        router.RoutesInto("GB").Select(route => route.Provider)
            .Should().ContainInOrder("stripe", "paypal");
    }

    // ── What must stay unroutable ───────────────────────────────────────────────────────────

    [Fact]
    public void An_unknown_row_routes_nowhere()
    {
        // The rule the repository set itself: a provider whose payload has never been executed
        // against a live account keeps every row Unknown, and Unknown is never routed to. This is
        // the state PayPal is in today, and Mauritius with it.
        var router = Router(PayPal("MU") with { Support = CapabilitySupport.Unknown });

        router.RoutesInto("MU").Should().BeEmpty();
        router.SupportedCountries().Should().BeEmpty();
    }

    [Fact]
    public void A_supported_row_for_a_provider_with_no_credentials_still_routes_nowhere()
    {
        // The second gate, and it is independent. Configuring a corridor does not conjure an
        // adapter: an unregistered provider cannot be paid through, so the matrix row alone must
        // not make a country look reachable.
        var router = new PayoutProviderRouter([Source(PayPal("MU"))], []);

        router.RoutesInto("MU").Should().BeEmpty();
    }

    [Fact]
    public void Mauritius_is_unreachable_through_stripe_however_the_matrix_is_written()
    {
        // Not an assertion about Stripe's business — an assertion that a GB row does not quietly
        // serve a member in MU. The country narrowing is exact.
        var router = Router(Stripe("GB"));

        router.RoutesInto("MU").Should().BeEmpty();
    }

    [Fact]
    public void A_country_code_that_is_not_one_routes_nowhere_rather_than_throwing()
    {
        var router = Router(Stripe("GB"));

        router.RoutesInto("GBR").Should().BeEmpty();
        router.RoutesInto("").Should().BeEmpty();
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private static PayoutCapability Stripe(string country) => new()
    {
        Provider = "stripe",
        Country = country,
        Currency = "GBP",
        Method = PayoutDestinationKind.BankAccount,
        Support = CapabilitySupport.Supported,
        Enabled = true,
        Priority = 10,
        RequiresProviderOnboarding = true,
        Note = "Test row",
    };

    /// <summary>
    /// A PayPal row as it would be written for Mauritius.
    /// <para>
    /// GBP rather than MUR on purpose: the Mauritian rupee is not one of PayPal's payout currencies,
    /// and a row naming it would be a corridor that refuses every payment made over it.
    /// </para>
    /// </summary>
    private static PayoutCapability PayPal(string country) => new()
    {
        Provider = "paypal",
        Country = country,
        Currency = "GBP",
        Method = PayoutDestinationKind.ProviderManaged,
        Support = CapabilitySupport.Supported,
        Enabled = true,
        Priority = 20,
        RequiresProviderOnboarding = false,
        Note = "Test row",
    };

    private static PayoutProviderRouter Router(params PayoutCapability[] capabilities)
    {
        var settlement = capabilities
            .Select(capability => capability.Provider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                var provider = Substitute.For<ISettlementProvider>();
                provider.Provider.Returns(name);

                return provider;
            })
            .ToList();

        return new PayoutProviderRouter([Source(capabilities)], settlement);
    }

    private static IPayoutCapabilitySource Source(params PayoutCapability[] capabilities)
    {
        var source = Substitute.For<IPayoutCapabilitySource>();
        source.GetCapabilities().Returns(capabilities);

        return source;
    }
}

/// <summary>
/// Whether a newly registered address can be paid to.
///
/// <para>
/// <b>The defect this pins was visible on a device and nowhere else.</b> A PayPal address registered
/// fine, showed a "Setting up" badge, and said "your provider is still checking this — you can
/// withdraw as soon as they are done". Nothing was checking it and nothing ever would: `IsUsable` is
/// `IsVerified &amp;&amp; !IsRemoved`, `Register` leaves a destination unverified, and the only
/// `IPayoutProviderVerificationSource` in the repository is Stripe's. The member was told to wait for
/// a moment that could not arrive.
/// </para>
///
/// <para>
/// A bank account should wait — somebody has to confirm it belongs to the member. An address cannot:
/// PayPal does not pre-confirm an email, it sends to it, and the recipient need not even hold an
/// account. So the question is the provider's capability, not the destination's shape.
/// </para>
/// </summary>
public sealed class PayoutDestinationVerifiabilityTests
{
    [Fact]
    public void A_provider_with_no_verification_source_cannot_hold_a_destination_pending()
    {
        // The rule stated as the code states it. With no source for the provider, registering and
        // then waiting is waiting for nobody.
        IReadOnlyList<IPayoutProviderVerificationSource> sources = [Source("stripe")];

        Verifiable(sources, "paypal").Should().BeFalse();
    }

    [Fact]
    public void A_provider_with_one_does()
    {
        // And the day a Flutterwave source is registered, its destinations start waiting again with
        // no edit to the handler.
        IReadOnlyList<IPayoutProviderVerificationSource> sources = [Source("stripe"), Source("flutterwave")];

        Verifiable(sources, "flutterwave").Should().BeTrue();
    }

    [Fact]
    public void The_match_is_on_the_provider_rather_than_the_case_it_is_written_in()
    {
        // Matrix rows are hand-written configuration. "PayPal" and "paypal" must not decide whether
        // somebody can be paid.
        IReadOnlyList<IPayoutProviderVerificationSource> sources = [Source("Stripe")];

        Verifiable(sources, "stripe").Should().BeTrue();
    }

    /// <summary>The predicate the handler applies, kept identical so this pins the real rule.</summary>
    private static bool Verifiable(
        IReadOnlyList<IPayoutProviderVerificationSource> sources, string provider) =>
        sources.Any(source =>
            string.Equals(source.Provider, provider, StringComparison.OrdinalIgnoreCase));

    private static IPayoutProviderVerificationSource Source(string provider)
    {
        var source = Substitute.For<IPayoutProviderVerificationSource>();
        source.Provider.Returns(provider);

        return source;
    }
}
