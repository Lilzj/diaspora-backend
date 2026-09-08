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
/// <c>paypal / MU / GBP</c>, never MUR. None of that is switched on here: the adapter has never been
/// executed against a live account, and the matrix keeps every row for it <c>Unknown</c> until it
/// has. These pin the routing decision so the day the row is added it behaves as intended.
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
