using Diaspora.Operations.Policies;
using Diaspora.Operations.Resilience;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Operations;

/// <summary>
/// One payment provider going down is one provider going down.
/// <para>
/// <b>The executor keys breaker state by the dependency name it is handed</b>, so what a name
/// covers decides what an outage covers. Until this change every client of all three payment
/// adapters was governed under one name — eleven clients, three providers, one breaker — and
/// <c>IPaymentGatewayRegistry.Resolve</c> picks a gateway by currency and country, so the three are
/// alternatives rather than a chain. A slow Flutterwave would have refused a Stripe charge that was
/// never going to touch it.
/// </para>
/// <para>
/// These run against the <see cref="PlatformResiliencePolicyContributor"/> the host composes rather
/// than against policies invented here, because the claim is about the numbers the platform ships.
/// </para>
/// </summary>
public sealed class PaymentBreakerIsolationTests
{
    private const string Stripe = OperationalPolicyNames.CircuitBreaker.StripePayments;
    private const string PayPal = OperationalPolicyNames.CircuitBreaker.PayPalPayments;
    private const string Flutterwave = OperationalPolicyNames.CircuitBreaker.FlutterwavePayments;

    /// <summary>
    /// <b>The anti-vacuity test, and it comes first because every other one here depends on it.</b>
    /// "An outage at one provider does not break the others" is satisfied forever by a breaker that
    /// never opens — by a sampling window that swallows the failures, by a classifier that does not
    /// count them, by a policy lookup that silently resolves something laxer. If the shipped
    /// settings stop tripping on this sequence, this fails and the isolation tests below become
    /// claims about nothing.
    /// </summary>
    [Fact]
    public async Task The_payment_breaker_opens_on_a_run_of_failures()
    {
        var executor = Build(out _);

        await FailAsync(executor, Stripe, times: 5);

        var call = async () => await executor.ExecuteAsync(
            Stripe, _ => Task.FromResult(1));

        await call.Should().ThrowAsync<CircuitOpenException>(
            "the shipped payment policy breaks at a 0.3 failure ratio over a minimum throughput " +
            "of 5, and that is five failures out of five");
    }

    /// <summary>
    /// <b>The claim.</b> Stripe failing is Stripe failing.
    /// </summary>
    [Fact]
    public async Task An_outage_at_one_payment_provider_leaves_the_others_serving()
    {
        var executor = Build(out _);

        await FailAsync(executor, Stripe, times: 5);

        var paypal = await executor.ExecuteAsync(PayPal, _ => Task.FromResult("charged"));
        var flutterwave = await executor.ExecuteAsync(Flutterwave, _ => Task.FromResult("charged"));

        paypal.Should().Be("charged");
        flutterwave.Should().Be("charged");

        executor.Find(Stripe)!.State.Should().Be(CircuitState.Open);
        executor.Find(PayPal)!.State.Should().Be(
            CircuitState.Closed, "a European card has nothing to do with an African corridor");
    }

    /// <summary>
    /// <b>The operator's half of the same property.</b> Isolating a provider before its maintenance
    /// window is the lever the dashboard exposes; under one shared name it would have taken the
    /// whole payment surface down, which is the opposite of what an operator reaching for it wants.
    /// </summary>
    [Fact]
    public async Task Isolating_one_payment_provider_leaves_the_others_serving()
    {
        var executor = Build(out _);

        executor.Isolate(Stripe);

        var refused = async () => await executor.ExecuteAsync(Stripe, _ => Task.FromResult(1));
        await refused.Should().ThrowAsync<CircuitOpenException>();

        var served = await executor.ExecuteAsync(PayPal, _ => Task.FromResult(1));
        served.Should().Be(1);
    }

    /// <summary>
    /// <b>Each provider resolves the strict payment breaker, not the external-API fallback.</b>
    /// Splitting one name into three is only an improvement if all three keep the settings the
    /// payment breaker was given; a dependency with no policy of its own silently inherits
    /// <c>external.api</c>, which breaks later and recovers sooner.
    /// </summary>
    [Theory]
    [InlineData(Stripe)]
    [InlineData(PayPal)]
    [InlineData(Flutterwave)]
    public void Every_payment_dependency_resolves_the_strict_payment_breaker(string dependency)
    {
        var catalog = Catalog();

        var breaker = catalog.Find<CircuitBreakerPolicy>(dependency);
        var fallback = catalog.Find<CircuitBreakerPolicy>(
            OperationalPolicyNames.CircuitBreaker.ExternalApi)!;

        breaker.Should().NotBeNull("'{0}' must declare its own breaker", dependency);
        breaker!.FailureRatio.Should().BeLessThan(
            fallback.FailureRatio, "a payment gateway breaks sooner than an ordinary external API");
        breaker.BreakDuration.Should().BeGreaterThan(
            fallback.BreakDuration, "and stays broken longer; it must not be hammered");
    }

    /// <summary>
    /// <b>And none of them is retried at the transport.</b> The executor falls back to
    /// <c>platform.default</c> — three attempts — for a dependency that declares no retry policy,
    /// so splitting one name into three created three new chances to gain two retries on a charge.
    /// <see cref="ResilientHttpHandler"/> refuses to be constructed in that state; this says the
    /// same thing about the catalogue the host will actually compose.
    /// </summary>
    [Theory]
    [InlineData(Stripe)]
    [InlineData(PayPal)]
    [InlineData(Flutterwave)]
    [InlineData(OperationalPolicyNames.CircuitBreaker.FirebasePush)]
    public void Every_governed_dependency_declares_a_zero_retry_policy(string dependency)
    {
        var retry = Catalog().Find<RetryPolicy>(dependency);

        retry.Should().NotBeNull(
            "'{0}' is governed by the handler, and a dependency with no retry policy falls back " +
            "to '{1}'", dependency, OperationalPolicyNames.Retry.Default);

        retry!.MaxAttempts.Should().Be(
            0, "the handler re-sends one HttpRequestMessage, and a charge is not idempotent");
    }

    // ── Setup ────────────────────────────────────────────────────────────

    private static async Task FailAsync(ResilienceExecutor executor, string dependency, int times)
    {
        for (var attempt = 0; attempt < times; attempt++)
        {
            var call = async () => await executor.ExecuteAsync<int>(
                dependency, _ => throw new TimeoutException("the provider is slow"));

            await call.Should().ThrowAsync<TimeoutException>();
        }
    }

    private static OperationalPolicyCatalog Catalog() =>
        new OperationalPolicyCatalog([new PlatformResiliencePolicyContributor()]);

    private static ResilienceExecutor Build(out FakeClock clock)
    {
        clock = new FakeClock();

        return new ResilienceExecutor(
            Catalog(),
            [new DefaultFailureClassifier()],
            clock,
            NullLogger<ResilienceExecutor>.Instance);
    }
}
