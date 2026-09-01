using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Modules.Financial.Infrastructure.Payouts;
using Diaspora.Modules.Financial.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// The payout capability matrix as it actually ships in <c>appsettings.Production.json</c>.
/// <para>
/// <b>A row that fails to bind is dropped and logged, never thrown</b> — which is the right
/// behaviour at runtime and a silent hole in a test suite. A typo in a country code would leave the
/// matrix quietly one corridor shorter, <c>PayoutsEnabled</c> would stay true, and the only person
/// to discover it would be the member whose withdrawal was refused. So the shipped file is parsed
/// here and counted.
/// </para>
/// <para>
/// <b>Every Supported row must cite where it was confirmed.</b> That is the rule in
/// <c>docs/financial/payout-routing.md</c>, and it exists because "Supported" is a claim somebody
/// has to be able to re-check later. A guessed row is indistinguishable from a confirmed one at
/// runtime and differs only when someone's money is involved.
/// </para>
/// </summary>
public class ShippedPayoutMatrixTests
{
    [Fact]
    public void Every_configured_row_binds()
    {
        var (options, capabilities) = Load();

        capabilities.Should().HaveCount(
            options.Capabilities.Count,
            "a row that fails to bind is dropped and logged rather than thrown, so a typo would "
            + "silently shorten the matrix");
    }

    [Fact]
    public void Every_supported_row_says_where_it_was_confirmed()
    {
        var (_, capabilities) = Load();

        capabilities
            .Where(capability => capability.Support == CapabilitySupport.Supported)
            .Should().OnlyContain(
                capability => !string.IsNullOrWhiteSpace(capability.Note),
                "a Supported row without a source cannot be re-checked, and is indistinguishable "
                + "from a guess");
    }

    [Fact]
    public void No_row_claims_support_for_a_provider_that_does_not_ship()
    {
        var (_, capabilities) = Load();

        capabilities.Select(capability => capability.Provider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Should().BeSubsetOf(
                ["stripe", "flutterwave", "paypal"],
                "a matrix naming an adapter nobody registered refuses with "
                + "financial.payout.provider_not_registered, which is a misconfiguration");
    }

    [Theory]
    // Stripe's own regions. Cross-border Connect payouts are limited to US/UK/EEA/CA/CH.
    [InlineData("stripe", "GB")]
    [InlineData("stripe", "US")]
    [InlineData("stripe", "DE")]
    // Flutterwave carries the corridors Stripe does not reach.
    [InlineData("flutterwave", "NG")]
    [InlineData("flutterwave", "GH")]
    [InlineData("flutterwave", "KE")]
    [InlineData("flutterwave", "ZA")]
    public void The_expected_corridors_are_routable(string provider, string country)
    {
        var router = Router(provider);

        router.SupportedCountries().Should().Contain(country);
    }

    [Fact]
    public void A_country_neither_provider_publishes_is_not_routable()
    {
        // Mauritius is the deployment's own wallet currency and appears in neither provider's
        // published corridors. The router refuses with financial.payout.country_unsupported, and
        // that refusal is correct — inventing a row to make the screen look finished would be
        // discovered by the first member who tried to withdraw.
        var router = Router("stripe", "flutterwave");

        router.SupportedCountries().Should().NotContain("MU");
    }

    [Fact]
    public void Payouts_are_off_until_an_adapter_is_actually_registered()
    {
        // The capabilities endpoint reports the INTERSECTION of a registered settlement provider and
        // a non-empty matrix. A matrix full of countries with no adapter behind them can pay nobody,
        // and saying otherwise would light up a withdraw button over nothing.
        var router = Router();

        router.SupportedCountries().Should().BeEmpty(
            "no settlement provider is registered in this fixture");
    }

    [Fact]
    public void Stripe_does_not_claim_a_corridor_outside_its_published_regions()
    {
        var (_, capabilities) = Load();

        // Stripe Connect cross-border payouts reach US, UK, EEA, Canada and Switzerland only.
        string[] stripeRegions =
        [
            "US", "GB", "CA", "CH",
            "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU", "IE",
            "IT", "LV", "LI", "LT", "LU", "MT", "NL", "NO", "PL", "PT", "RO", "SK", "SI", "ES", "SE",
        ];

        capabilities
            .Where(capability => capability.Provider.Equals("stripe", StringComparison.OrdinalIgnoreCase))
            .Select(capability => capability.Country)
            .Should().BeSubsetOf(
                stripeRegions,
                "Stripe does not support self-serve cross-border payouts outside those regions");
    }

    [Fact]
    public void The_base_settings_file_declares_no_corridor()
    {
        // Configuration arrays merge BY INDEX rather than being replaced. Every host loads
        // appsettings.json, and the integration harness overrides Capabilities:0 with five keys for
        // its fake adapter — so any key the harness does NOT set survives from the base file into
        // its row. A single seeded row here set RequiresProviderOnboarding on the fake provider and
        // turned 22 withdrawal tests into provider_onboarding_required refusals.
        //
        // The shipped matrix therefore lives in the Production-only file, and this stays empty.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(SettingsPath("appsettings.json"))
            .Build();

        var options = new PayoutRoutingOptions();
        configuration.GetSection(PayoutRoutingOptions.SectionName).Bind(options);

        options.Capabilities.Should().BeEmpty(
            "a row in the base file bleeds its unset keys into every environment that overrides "
            + "Capabilities:0");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (PayoutRoutingOptions Options, IReadOnlyList<PayoutCapability> Capabilities) Load()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(ShippedSettingsPath())
            .Build();

        var options = new PayoutRoutingOptions();
        configuration.GetSection(PayoutRoutingOptions.SectionName).Bind(options);

        var source = new ConfiguredPayoutCapabilitySource(
            new StaticRouting(options),
            NullLogger<ConfiguredPayoutCapabilitySource>.Instance);

        return (options, source.GetCapabilities());
    }

    private static PayoutProviderRouter Router(params string[] registeredProviders)
    {
        var (options, _) = Load();

        return new PayoutProviderRouter(
            [
                new ConfiguredPayoutCapabilitySource(
                    new StaticRouting(options),
                    NullLogger<ConfiguredPayoutCapabilitySource>.Instance),
            ],
            registeredProviders.Select(name => (ISettlementProvider)new NamedProvider(name)));
    }

    private static string ShippedSettingsPath() => SettingsPath("appsettings.Production.json");

    private static string SettingsPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the repository root should be above the test binary");

        return Path.Combine(directory!.FullName, "src", "Diaspora.Api", fileName);
    }

    private sealed class StaticRouting(PayoutRoutingOptions value)
        : IOptionsMonitor<PayoutRoutingOptions>
    {
        public PayoutRoutingOptions CurrentValue => value;

        public PayoutRoutingOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<PayoutRoutingOptions, string?> listener) => null;
    }

    /// <summary>A settlement provider that exists only to be named. The router asks nothing else.</summary>
    private sealed class NamedProvider(string provider) : ISettlementProvider
    {
        public string Provider => provider;

        public Task<SettlementOutcome> SubmitAsync(
            SettlementInstruction instruction, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("routing only");
    }
}
