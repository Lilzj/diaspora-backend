using Diaspora.Sms;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Sms;

/// <summary>
/// Which wire carries which number.
/// <para>
/// The routing table is the entire reason this platform configures two SMS providers rather than
/// one. A Nigerian number on the global provider costs several times what it costs on the
/// regional one; a German number on the regional one does not arrive at all. Every test here is
/// about a way that choice can go quietly wrong — quietly being the operative word, because the
/// symptom of a bad route is a member who never receives a code and therefore cannot report it.
/// </para>
/// </summary>
public sealed class SmsRoutingTests
{
    private const string Nigerian = "+2348100200001";
    private const string German = "+4915112345678";
    private const string American = "+12025550142";
    private const string Jamaican = "+18765550142";

    [Fact]
    public void A_routed_prefix_goes_to_the_provider_that_claims_it()
    {
        var route = Router(Options(
            defaultProvider: SmsProviderNames.Twilio,
            routes: [Route(SmsProviderNames.AfricasTalking, "+234", "+254")]))
            .Resolve(Nigerian);

        route!.Primary.Provider.Should().Be(SmsProviderNames.AfricasTalking);
    }

    [Fact]
    public void An_unclaimed_destination_goes_to_the_default_provider()
    {
        // The half of the world nobody wrote a route for. On a global platform this is the
        // majority of destinations, which is why an absent default is a validation failure.
        var route = Router(Options(
            defaultProvider: SmsProviderNames.Twilio,
            routes: [Route(SmsProviderNames.AfricasTalking, "+234")]))
            .Resolve(German);

        route!.Primary.Provider.Should().Be(SmsProviderNames.Twilio);
    }

    [Fact]
    public void The_longest_matching_prefix_wins_regardless_of_the_order_they_were_written()
    {
        // +1 is the United States, Canada and much of the Caribbean; +1876 is Jamaica. A
        // first-match table would make this depend on which line an operator happened to type
        // first, and the failure would show up as one country's members never getting a code.
        var options = Options(
            defaultProvider: SmsProviderNames.Twilio,
            routes:
            [
                Route(SmsProviderNames.Twilio, "+1"),
                Route(SmsProviderNames.AfricasTalking, "+1876")
            ]);

        Router(options).Resolve(Jamaican)!.Primary.Provider
            .Should().Be(SmsProviderNames.AfricasTalking);

        Router(options).Resolve(American)!.Primary.Provider
            .Should().Be(SmsProviderNames.Twilio);
    }

    [Fact]
    public void The_order_of_the_routes_does_not_change_the_answer()
    {
        // The same table, written the other way round. Stated separately from the test above
        // because "longest wins" and "order is irrelevant" are different claims, and a table that
        // sorted its input would pass the first while still failing this one.
        var reversed = Options(
            defaultProvider: SmsProviderNames.Twilio,
            routes:
            [
                Route(SmsProviderNames.AfricasTalking, "+1876"),
                Route(SmsProviderNames.Twilio, "+1")
            ]);

        Router(reversed).Resolve(Jamaican)!.Primary.Provider
            .Should().Be(SmsProviderNames.AfricasTalking);
    }

    [Theory]
    [InlineData("+234")]
    [InlineData("234")]
    [InlineData("+234 ")]
    [InlineData("+2-3-4")]
    public void A_prefix_matches_however_it_was_typed(string written)
    {
        // A routing table is edited by hand, in a JSON file, at speed. A leading plus or a stray
        // space silently failing to match would route Nigeria to the expensive provider and
        // nothing anywhere would say so.
        var route = Router(Options(
            defaultProvider: SmsProviderNames.Twilio,
            routes: [Route(SmsProviderNames.AfricasTalking, written)]))
            .Resolve(Nigerian);

        route!.Primary.Provider.Should().Be(SmsProviderNames.AfricasTalking);
    }

    [Fact]
    public void A_route_to_an_unconfigured_provider_falls_through_rather_than_dropping_the_message()
    {
        // Somebody listed the regional provider in Routes but never filled in its credentials.
        // Carrying the code on the expensive wire is the right failure: it costs money, and the
        // member signs in.
        var route = Router(
            Options(
                defaultProvider: SmsProviderNames.Twilio,
                routes: [Route(SmsProviderNames.AfricasTalking, "+234")]),
            africasTalkingConfigured: false)
            .Resolve(Nigerian);

        route!.Primary.Provider.Should().Be(SmsProviderNames.Twilio);
    }

    [Fact]
    public void A_deployment_with_nothing_configured_resolves_to_nothing()
    {
        // Null, not an exception. "No SMS provider" is a legitimate development state, and the
        // caller turns it into a delivery failure the member's screen can explain — where an
        // exception here would surface as a 500 on a sign-in.
        var route = Router(
            Options(defaultProvider: SmsProviderNames.Twilio),
            twilioConfigured: false,
            africasTalkingConfigured: false)
            .Resolve(Nigerian);

        route.Should().BeNull();
    }

    [Fact]
    public void The_fallback_is_the_default_provider_when_the_route_chose_another()
    {
        var route = Router(Options(
            defaultProvider: SmsProviderNames.Twilio,
            routes: [Route(SmsProviderNames.AfricasTalking, "+234")]))
            .Resolve(Nigerian);

        route!.Fallback!.Provider.Should().Be(SmsProviderNames.Twilio);
    }

    [Fact]
    public void A_destination_already_on_the_default_provider_has_no_fallback()
    {
        // Falling back to the wire that just failed is two attempts at the same provider wearing
        // different names. It doubles the cost of an outage and improves nothing.
        var route = Router(Options(defaultProvider: SmsProviderNames.Twilio)).Resolve(German);

        route!.Primary.Provider.Should().Be(SmsProviderNames.Twilio);
        route.Fallback.Should().BeNull();
    }

    [Fact]
    public void Turning_the_fallback_off_leaves_one_wire()
    {
        var options = Options(
            defaultProvider: SmsProviderNames.Twilio,
            routes: [Route(SmsProviderNames.AfricasTalking, "+234")]);

        options.FallBackToDefaultProvider = false;

        Router(options).Resolve(Nigerian)!.Fallback.Should().BeNull();
    }

    // ── Fixtures ────────────────────────────────────────────────────────────

    private static SmsRouteOptions Route(string provider, params string[] codes) =>
        new() { Provider = provider, CallingCodes = [.. codes] };

    private static SmsOptions Options(
        string defaultProvider,
        IList<SmsRouteOptions>? routes = null) =>
        new()
        {
            DefaultProvider = defaultProvider,
            Routes = routes ?? []
        };

    private static SmsGatewayRouter Router(
        SmsOptions options,
        bool twilioConfigured = true,
        bool africasTalkingConfigured = true) =>
        new(
            [
                new StubGateway(SmsProviderNames.Twilio, twilioConfigured),
                new StubGateway(SmsProviderNames.AfricasTalking, africasTalkingConfigured)
            ],
            new StaticOptions(options));

    private sealed class StubGateway(string provider, bool configured) : ISmsGateway
    {
        public string Provider { get; } = provider;

        public bool IsConfigured { get; } = configured;

        public Task<SmsDispatch> SendAsync(SmsMessage message, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SmsDispatch(Provider, "stub"));
    }

    private sealed class StaticOptions(SmsOptions value) : IOptionsMonitor<SmsOptions>
    {
        public SmsOptions CurrentValue { get; } = value;

        public SmsOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<SmsOptions, string?> listener) => null;
    }
}
