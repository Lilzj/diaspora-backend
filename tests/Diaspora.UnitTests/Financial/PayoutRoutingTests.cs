using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Modules.Financial.Infrastructure.Payouts;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// Which provider may carry a payout, and why none may.
/// <para>
/// <b>Every provider in this file is invented, and that is deliberate.</b> The adapters are called
/// alpha, beta, gamma and delta because a fixture row reading "this vendor settles bank payouts in
/// Nigeria" is a capability claim, and a test fixture is the last place a capability claim should
/// live — somebody would copy it into configuration. What is under test is the router's behaviour
/// given a matrix, never the contents of any real matrix. The production matrix is configuration and
/// every row in it has to be confirmed against provider documentation before it says
/// <see cref="CapabilitySupport.Supported"/>.
/// </para>
/// <para>
/// <b>The twelve countries are the ones the brief names</b> — Mauritius, Nigeria, Ghana, Kenya,
/// South Africa, Rwanda, Tanzania, Uganda, Côte d'Ivoire, the UK, the US and India — and the fixture
/// arranges them so that between them they reach every refusal the router can give. A country
/// appearing here is not a country the platform pays into.
/// </para>
/// </summary>
public class PayoutRoutingTests
{
    // ── The fixture matrix. Invented — see the class summary. ────────────────────────────────

    private const string Alpha = "alpha";
    private const string Beta = "beta";
    private const string Gamma = "gamma";

    /// <summary>Named in the matrix, deliberately never registered as an adapter.</summary>
    private const string Delta = "delta";

    private static IReadOnlyList<PayoutCapability> Matrix() =>
    [
        Row(Alpha, "NG", PayoutDestinationKind.BankAccount, "NGN"),
        Row(Beta, "KE", PayoutDestinationKind.MobileMoney, "KES"),
        Row(Beta, "GH", PayoutDestinationKind.MobileMoney, "GHS"),
        Row(Alpha, "ZA", PayoutDestinationKind.BankAccount, "ZAR"),
        Row(Alpha, "GB", PayoutDestinationKind.BankAccount, "GBP"),
        Row(Alpha, "US", PayoutDestinationKind.BankAccount, "USD"),

        // Nobody has confirmed this one against the provider's documentation.
        Row(Gamma, "IN", PayoutDestinationKind.BankAccount, "INR") with
        {
            Support = CapabilitySupport.Unknown,
            Note = "Provider lists India; payout rails for it are not documented."
        },

        // Checked, and the provider does not offer it. Carried so the check is visible.
        Row(Beta, "RW", PayoutDestinationKind.MobileMoney, "RWF") with
        {
            Support = CapabilitySupport.Unsupported
        },

        // Capable, and this deployment has not switched it on.
        Row(Beta, "TZ", PayoutDestinationKind.MobileMoney, "TZS") with { Enabled = false },

        // Capable, and the provider will not pay an unverified payee.
        Row(Beta, "UG", PayoutDestinationKind.MobileMoney, "UGX") with
        {
            RequiresVerifiedPayee = true
        },

        // A supported route through an adapter this deployment never registered.
        Row(Delta, "CI", PayoutDestinationKind.BankAccount, "XOF")

        // Mauritius is absent on purpose. The platform's own jurisdiction is not automatically a
        // payout destination, and the router must say so rather than assume.
    ];

    private static PayoutCapability Row(
        string provider, string country, PayoutDestinationKind method, string currency) =>
        new()
        {
            Provider = provider,
            Country = country,
            Method = method,
            Currency = currency,
            Support = CapabilitySupport.Supported
        };

    // ── The country matrix the brief asks for ────────────────────────────────────────────────

    /// <summary>
    /// <b>The router's answer for each of the twelve named countries.</b> Every refusal reason is
    /// reached by at least one of them, which is the point of the arrangement: a router that
    /// collapsed these into one "unavailable" would pass a test that only checked the supported
    /// cases.
    /// </summary>
    [Theory]
    // Routed.
    [InlineData("NG", PayoutDestinationKind.BankAccount, "NGN", true, PayoutRefusalReason.None)]
    [InlineData("KE", PayoutDestinationKind.MobileMoney, "KES", true, PayoutRefusalReason.None)]
    [InlineData("GH", PayoutDestinationKind.MobileMoney, "GHS", true, PayoutRefusalReason.None)]
    [InlineData("ZA", PayoutDestinationKind.BankAccount, "ZAR", true, PayoutRefusalReason.None)]
    [InlineData("GB", PayoutDestinationKind.BankAccount, "GBP", true, PayoutRefusalReason.None)]
    [InlineData("US", PayoutDestinationKind.BankAccount, "USD", true, PayoutRefusalReason.None)]
    // Refused, each for its own reason.
    [InlineData("MU", PayoutDestinationKind.BankAccount, "MUR", true, PayoutRefusalReason.CountryUnsupported)]
    [InlineData("RW", PayoutDestinationKind.MobileMoney, "RWF", true, PayoutRefusalReason.CountryUnsupported)]
    [InlineData("IN", PayoutDestinationKind.BankAccount, "INR", true, PayoutRefusalReason.CapabilityUnknown)]
    [InlineData("TZ", PayoutDestinationKind.MobileMoney, "TZS", true, PayoutRefusalReason.RouteDisabled)]
    [InlineData("UG", PayoutDestinationKind.MobileMoney, "UGX", false, PayoutRefusalReason.PayeeVerificationRequired)]
    [InlineData("CI", PayoutDestinationKind.BankAccount, "XOF", true, PayoutRefusalReason.ProviderNotRegistered)]
    public void The_router_answers_each_country_with_the_reason_that_fits_it(
        string country,
        PayoutDestinationKind method,
        string currency,
        bool payeeVerified,
        PayoutRefusalReason expected)
    {
        var result = Router().Resolve(Request(country, method, currency, payeeVerified: payeeVerified));

        result.Refusal.Should().Be(expected);
        result.IsRouted.Should().Be(expected == PayoutRefusalReason.None);
    }

    /// <summary>
    /// Rwanda's only row says the provider does not offer it, so the country drops out entirely
    /// rather than surfacing as a rail or currency problem. The row stays in the matrix so an
    /// operator can see somebody checked; it is never a candidate.
    /// </summary>
    [Fact]
    public void A_documented_unsupported_row_is_never_selected()
    {
        var result = Router().Resolve(Request("RW", PayoutDestinationKind.MobileMoney, "RWF"));

        result.IsRouted.Should().BeFalse();
        result.Refusal.Should().Be(PayoutRefusalReason.CountryUnsupported);
    }

    // ── The rule the brief is most insistent about ───────────────────────────────────────────

    /// <summary>
    /// <b>An unconfirmed capability is never routed to, and never reported as unsupported.</b> This
    /// is the difference between "the provider cannot pay into India" and "nobody has checked
    /// whether it can" — the first is a fact about a vendor, the second is a job for whoever owns
    /// the matrix, and a system that conflated them would quietly stop anyone doing that job.
    /// </summary>
    [Fact]
    public void An_unconfirmed_capability_is_refused_as_unknown_and_carries_the_open_question()
    {
        var result = Router().Resolve(Request("IN", PayoutDestinationKind.BankAccount, "INR"));

        result.IsRouted.Should().BeFalse();
        result.Refusal.Should().Be(PayoutRefusalReason.CapabilityUnknown);
        result.Detail.Should().Contain("not documented");
        result.Refusal.ToErrorCode().Should().Be(FinancialErrorCodes.PayoutCapabilityUnknown);
    }

    /// <summary>
    /// A row nobody filled in the support column for defaults to <c>Unknown</c> rather than
    /// <c>Supported</c>. The default is the one that fails closed.
    /// </summary>
    [Fact]
    public void A_capability_row_defaults_to_unknown()
    {
        var bare = new PayoutCapability
        {
            Provider = Alpha,
            Country = "NG",
            Method = PayoutDestinationKind.BankAccount,
            Currency = "NGN"
        };

        bare.Support.Should().Be(CapabilitySupport.Unknown);
        bare.IsRoutable.Should().BeFalse();
    }

    // ── Narrowing: each refusal is the one worth acting on ───────────────────────────────────

    [Fact]
    public void A_served_country_refuses_a_rail_it_does_not_serve()
    {
        // Nigeria is served over a bank account, not over mobile money.
        var result = Router().Resolve(Request("NG", PayoutDestinationKind.MobileMoney, "NGN"));

        result.Refusal.Should().Be(PayoutRefusalReason.MethodUnsupported);
    }

    [Fact]
    public void A_served_rail_refuses_a_currency_it_does_not_settle()
    {
        var result = Router().Resolve(Request("NG", PayoutDestinationKind.BankAccount, "USD"));

        result.Refusal.Should().Be(PayoutRefusalReason.CurrencyUnsupported);
    }

    /// <summary>
    /// The router never converts. A payout denominated in a currency the route does not settle is a
    /// different transaction, not a cheaper one, and <see cref="Money"/> refuses to combine the two
    /// anywhere else in the platform for the same reason.
    /// </summary>
    [Fact]
    public void The_router_does_not_reach_for_a_convertible_route()
    {
        var result = Router().Resolve(Request("KE", PayoutDestinationKind.MobileMoney, "USD"));

        result.Refusal.Should().Be(PayoutRefusalReason.CurrencyUnsupported);
    }

    [Fact]
    public void An_empty_matrix_refuses_every_country()
    {
        var router = new PayoutProviderRouter([new StubSource([])], [new StubSettlement(Alpha)]);

        router.Resolve(Request("GB", PayoutDestinationKind.BankAccount, "GBP"))
            .Refusal.Should().Be(PayoutRefusalReason.CountryUnsupported);
    }

    [Fact]
    public void A_country_code_that_is_not_a_country_code_is_refused_rather_than_matched()
    {
        var result = Router().Resolve(Request("NGA", PayoutDestinationKind.BankAccount, "NGN"));

        result.Refusal.Should().Be(PayoutRefusalReason.CountryUnsupported);
        result.Detail.Should().Contain("ISO 3166-1");
    }

    [Fact]
    public void Country_and_currency_codes_match_regardless_of_case_or_padding()
    {
        var result = Router().Resolve(Request(" ng ", PayoutDestinationKind.BankAccount, "NGN"));

        result.IsRouted.Should().BeTrue("a routing table is edited by hand");
        result.Provider.Should().Be(Alpha);
    }

    // ── Limits and verification ──────────────────────────────────────────────────────────────

    [Fact]
    public void An_amount_below_the_route_minimum_is_refused_with_the_limits_named()
    {
        var router = RouterWith(Row(Alpha, "GB", PayoutDestinationKind.BankAccount, "GBP") with
        {
            MinimumMinorUnits = 5_000,
            MaximumMinorUnits = 1_000_000
        });

        var result = router.Resolve(Request("GB", PayoutDestinationKind.BankAccount, "GBP", minorUnits: 100));

        result.Refusal.Should().Be(PayoutRefusalReason.AmountOutOfRange);
        result.Detail.Should().Contain("5000").And.Contain("1000000");
    }

    [Fact]
    public void An_amount_above_the_route_maximum_is_refused()
    {
        var router = RouterWith(Row(Alpha, "GB", PayoutDestinationKind.BankAccount, "GBP") with
        {
            MaximumMinorUnits = 1_000
        });

        router.Resolve(Request("GB", PayoutDestinationKind.BankAccount, "GBP", minorUnits: 1_001))
            .Refusal.Should().Be(PayoutRefusalReason.AmountOutOfRange);

        router.Resolve(Request("GB", PayoutDestinationKind.BankAccount, "GBP", minorUnits: 1_000))
            .IsRouted.Should().BeTrue("the maximum is inclusive");
    }

    /// <summary>
    /// Verification is reported before the amount, deliberately. Being unverified blocks every
    /// attempt until the member does something about it; an amount outside the limits blocks only
    /// this one, and telling them the transient thing first would have them fix it and hit the
    /// standing one anyway.
    /// </summary>
    [Fact]
    public void An_unverified_payee_hears_about_verification_before_the_amount()
    {
        var router = RouterWith(Row(Beta, "UG", PayoutDestinationKind.MobileMoney, "UGX") with
        {
            RequiresVerifiedPayee = true,
            MinimumMinorUnits = 100_000
        });

        var result = router.Resolve(
            Request("UG", PayoutDestinationKind.MobileMoney, "UGX", minorUnits: 5, payeeVerified: false));

        result.Refusal.Should().Be(PayoutRefusalReason.PayeeVerificationRequired);
    }

    [Fact]
    public void A_verified_payee_routes_where_an_unverified_one_could_not()
    {
        var request = Request("UG", PayoutDestinationKind.MobileMoney, "UGX", payeeVerified: true);

        Router().Resolve(request).IsRouted.Should().BeTrue();
        Router().Resolve(request with { PayeeVerified = false })
            .Refusal.Should().Be(PayoutRefusalReason.PayeeVerificationRequired);
    }

    // ── Misconfiguration is reported as itself ───────────────────────────────────────────────

    /// <summary>
    /// A matrix naming an adapter nobody registered is the platform's own mistake. Reporting it as
    /// a country or currency problem would send an operator to renegotiate coverage they already
    /// have.
    /// </summary>
    [Fact]
    public void A_route_through_an_unregistered_adapter_is_reported_as_misconfiguration()
    {
        var result = Router().Resolve(Request("CI", PayoutDestinationKind.BankAccount, "XOF"));

        result.Refusal.Should().Be(PayoutRefusalReason.ProviderNotRegistered);
        result.Detail.Should().Contain(Delta);
        result.Refusal.ToErrorCode().Should().Be(FinancialErrorCodes.PayoutProviderNotRegistered);
    }

    [Fact]
    public void A_supported_route_this_deployment_switched_off_is_reported_as_disabled()
    {
        var result = Router().Resolve(Request("TZ", PayoutDestinationKind.MobileMoney, "TZS"));

        result.Refusal.Should().Be(PayoutRefusalReason.RouteDisabled);
        result.Refusal.ToErrorCode().Should().Be(FinancialErrorCodes.PayoutRouteDisabled);
    }

    // ── Selection is deterministic ───────────────────────────────────────────────────────────

    [Fact]
    public void Priority_decides_between_two_providers_that_can_both_carry_it()
    {
        var router = RouterWith(
            Row(Alpha, "GB", PayoutDestinationKind.BankAccount, "GBP") with { Priority = 10 },
            Row(Beta, "GB", PayoutDestinationKind.BankAccount, "GBP") with { Priority = 1 });

        router.Resolve(Request("GB", PayoutDestinationKind.BankAccount, "GBP"))
            .Provider.Should().Be(Beta);
    }

    /// <summary>
    /// <b>Equal priority breaks on the provider's name, never on load order.</b> Without the
    /// tie-break the chosen provider would change when somebody reordered a registration block,
    /// which is a routing decision nobody would think they were making.
    /// </summary>
    [Fact]
    public void Equal_priority_breaks_on_name_so_registration_order_cannot_change_the_route()
    {
        var forwards = new PayoutProviderRouter(
            [new StubSource([
                Row(Alpha, "GB", PayoutDestinationKind.BankAccount, "GBP"),
                Row(Beta, "GB", PayoutDestinationKind.BankAccount, "GBP")])],
            Registered());

        var backwards = new PayoutProviderRouter(
            [new StubSource([
                Row(Beta, "GB", PayoutDestinationKind.BankAccount, "GBP"),
                Row(Alpha, "GB", PayoutDestinationKind.BankAccount, "GBP")])],
            Registered());

        var request = Request("GB", PayoutDestinationKind.BankAccount, "GBP");

        forwards.Resolve(request).Provider.Should().Be(Alpha);
        backwards.Resolve(request).Provider.Should().Be(Alpha);
    }

    /// <summary>
    /// <b>Alternates are the routes that also matched, and they are not a failover list.</b> A
    /// payout that failed may still have moved the money, so the alternate is usable only before
    /// anything has been submitted. The test pins that they are returned in preference order, so a
    /// caller choosing one before submission is making the same decision the router would have.
    /// </summary>
    [Fact]
    public void Alternates_come_back_in_preference_order_and_exclude_the_chosen_route()
    {
        var router = RouterWith(
            Row(Alpha, "GB", PayoutDestinationKind.BankAccount, "GBP") with { Priority = 3 },
            Row(Beta, "GB", PayoutDestinationKind.BankAccount, "GBP") with { Priority = 1 },
            Row(Gamma, "GB", PayoutDestinationKind.BankAccount, "GBP") with { Priority = 2 });

        var result = router.Resolve(Request("GB", PayoutDestinationKind.BankAccount, "GBP"));

        result.Provider.Should().Be(Beta);
        result.Alternates.Select(capability => capability.Provider)
            .Should().Equal(Gamma, Alpha);
    }

    [Fact]
    public void A_refused_result_has_no_provider_to_read()
    {
        var result = Router().Resolve(Request("MU", PayoutDestinationKind.BankAccount, "MUR"));

        var attempt = () => result.Provider;

        attempt.Should().Throw<InvalidOperationException>();
    }

    // ── Sources are additive, and disagreement resolves toward not paying ────────────────────

    /// <summary>
    /// <b>Two sources disagreeing about whether money can reach somewhere is a configuration
    /// conflict, and the only resolution that cannot lose money is the one that declines.</b> An
    /// operator who means to enable a route edits the source that says otherwise rather than
    /// outvoting it.
    /// </summary>
    [Fact]
    public void Where_two_sources_describe_one_route_the_more_cautious_row_wins()
    {
        var optimistic = new StubSource([Row(Alpha, "GB", PayoutDestinationKind.BankAccount, "GBP")]);

        var cautious = new StubSource([
            Row(Alpha, "GB", PayoutDestinationKind.BankAccount, "GBP") with
            {
                Support = CapabilitySupport.Unknown,
                Note = "Awaiting confirmation from the provider."
            }]);

        var router = new PayoutProviderRouter([optimistic, cautious], Registered());

        router.Resolve(Request("GB", PayoutDestinationKind.BankAccount, "GBP"))
            .Refusal.Should().Be(PayoutRefusalReason.CapabilityUnknown);
    }

    [Fact]
    public void A_tighter_limit_from_a_second_source_narrows_the_route()
    {
        var broad = new StubSource([
            Row(Alpha, "GB", PayoutDestinationKind.BankAccount, "GBP") with
            {
                MaximumMinorUnits = 1_000_000
            }]);

        var narrow = new StubSource([
            Row(Alpha, "GB", PayoutDestinationKind.BankAccount, "GBP") with
            {
                MaximumMinorUnits = 500
            }]);

        var router = new PayoutProviderRouter([broad, narrow], Registered());

        router.Resolve(Request("GB", PayoutDestinationKind.BankAccount, "GBP", minorUnits: 1_000))
            .Refusal.Should().Be(PayoutRefusalReason.AmountOutOfRange);
    }

    [Fact]
    public void A_verification_requirement_from_either_source_applies()
    {
        var lenient = new StubSource([Row(Beta, "KE", PayoutDestinationKind.MobileMoney, "KES")]);

        var strict = new StubSource([
            Row(Beta, "KE", PayoutDestinationKind.MobileMoney, "KES") with
            {
                RequiresVerifiedPayee = true
            }]);

        var router = new PayoutProviderRouter([lenient, strict], Registered());

        router.Resolve(Request("KE", PayoutDestinationKind.MobileMoney, "KES", payeeVerified: false))
            .Refusal.Should().Be(PayoutRefusalReason.PayeeVerificationRequired);
    }

    // ── The capability surface ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The countries a client may be told about are the ones that are supported, enabled
    /// <em>and</em> have a registered adapter behind them. A country listed on the strength of a
    /// matrix row alone would be one the platform advertises and then refuses.
    /// </summary>
    [Fact]
    public void The_supported_country_list_excludes_everything_that_would_refuse()
    {
        var countries = Router().SupportedCountries();

        countries.Should().Equal("GB", "GH", "KE", "NG", "UG", "US", "ZA");
        countries.Should().NotContain(["IN"], "an unconfirmed capability is not coverage");
        countries.Should().NotContain(["TZ"], "a disabled route is not coverage");
        countries.Should().NotContain(["CI"], "an unregistered adapter is not coverage");
        countries.Should().NotContain(["RW"], "a documented gap is not coverage");
        countries.Should().NotContain(["MU"], "the platform's own jurisdiction is not a destination");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private static PayoutProviderRouter Router() =>
        new([new StubSource(Matrix())], Registered());

    private static PayoutProviderRouter RouterWith(params PayoutCapability[] rows) =>
        new([new StubSource(rows)], Registered());

    private static ISettlementProvider[] Registered() =>
        [new StubSettlement(Alpha), new StubSettlement(Beta), new StubSettlement(Gamma)];

    private static PayoutRouteRequest Request(
        string country,
        PayoutDestinationKind method,
        string currency,
        long minorUnits = 10_000,
        bool payeeVerified = true) =>
        new()
        {
            Country = country,
            Method = method,
            Amount = Money.FromMinorUnits(minorUnits, currency),
            PayeeVerified = payeeVerified
        };

    private sealed class StubSource(IReadOnlyList<PayoutCapability> rows) : IPayoutCapabilitySource
    {
        public string Name => "stub";

        public IReadOnlyList<PayoutCapability> GetCapabilities() => rows;
    }

    private sealed class StubSettlement(string provider) : ISettlementProvider
    {
        public string Provider => provider;

        public Task<SettlementOutcome> SubmitAsync(
            SettlementInstruction instruction, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Routing must never submit. A test that reached this line was testing the wrong thing.");
    }
}
