using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Payments.Stripe;
using Diaspora.Operations.Policies;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// What <c>AddStripePayments</c> puts in the container, and what it must not.
/// <para>
/// <b>The registration is the security boundary, not the adapter.</b> An adapter registered without
/// credentials tells the capabilities endpoint that this deployment has a payment provider, which
/// the client draws a deposit button from; every payment then reaches Stripe with an empty key and
/// comes back 401, which reads to a member as a platform fault rather than a missing secret.
/// </para>
/// </summary>
public class StripeRegistrationTests
{
    [Fact]
    public void An_unconfigured_deployment_registers_no_payment_gateway_at_all()
    {
        using var provider = Build(secretKey: null);

        provider.GetServices<IPaymentGateway>().Should().BeEmpty(
            "an adapter with no key must not tell the capabilities endpoint that this deployment "
            + "can take payments");

        provider.GetServices<ISettlementProvider>().Should().BeEmpty(
            "and it must not tell the payout router that its rail is available");
    }

    [Fact]
    public void A_configured_deployment_registers_the_charge_half()
    {
        using var provider = Build(secretKey: "sk_test_stub");

        provider.GetServices<IPaymentGateway>()
            .Should().ContainSingle("exactly one Stripe gateway, registered explicitly")
            .Which.Should().BeOfType<StripePaymentGateway>();
    }

    [Fact]
    public void A_configured_deployment_registers_the_payment_callback_processor()
    {
        // Authorise and capture are separated by the member confirming the intent in the app. This
        // callback is the only thing that tells the platform that happened.
        using var provider = Build(secretKey: "sk_test_stub");

        provider.GetServices<IPaymentWebhookProcessor>()
            .Should().ContainSingle()
            .Which.Should().BeOfType<StripePaymentWebhookProcessor>();
    }

    [Fact]
    public void The_charge_half_and_the_payout_half_are_both_registered_from_one_key()
    {
        // They are separate seams and separate adapters, but one credential. A deployment that
        // configured Stripe expecting both and got one would find that out in production.
        using var provider = Build(secretKey: "sk_test_stub");

        provider.GetServices<IPaymentGateway>().Should().ContainSingle();
        provider.GetServices<ISettlementProvider>().Should().ContainSingle();
    }

    [Fact]
    public void The_gateway_is_a_singleton_so_its_named_http_client_is_reused()
    {
        // A scan-built registration would be container-activated and handed the default HttpClient
        // rather than the named one carrying the configured timeout. Resolving twice and getting the
        // same instance is what says this came from the explicit factory.
        using var provider = Build(secretKey: "sk_test_stub");

        var first = provider.GetRequiredService<IPaymentGateway>();
        var second = provider.GetRequiredService<IPaymentGateway>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void The_registered_gateway_declares_the_method_the_deposit_screen_reads()
    {
        // GetFinancialCapabilitiesQueryHandler builds `supportedMethods` from exactly this, and the
        // wallet's Add money button is gated on it being non-empty.
        using var provider = Build(secretKey: "sk_test_stub");

        var gateway = provider.GetRequiredService<IPaymentGateway>();

        gateway.Provider.Should().Be("stripe");
        gateway.SupportedMethods.Should().NotBeEmpty();
    }

    /// <summary>
    /// <b>A deployment that would retry a charge cannot create a Stripe client at all.</b> The
    /// executor resolves retry by dependency name and falls back to three attempts for a name that
    /// declares none, so the policy is what stands between a timing-out provider and a duplicate
    /// charge. Asserted through the real registration rather than against the handler directly,
    /// because the thing worth knowing is that the guard survives the DI graph the adapter is
    /// actually composed in.
    /// </summary>
    [Fact]
    public void A_policy_that_would_retry_a_charge_stops_the_client_being_created()
    {
        using var provider = Build(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Payments:Stripe:Provider"] = "stripe",
                ["Payments:Stripe:ApiBase"] = "https://stripe.test",
                ["Payments:Stripe:SecretKey"] = "sk_test_stub"
            },
            new RetryingPaymentPolicyContributor());

        var create = () => provider.GetServices<IPaymentGateway>().ToList();

        create.Should().Throw<InvalidOperationException>()
            .WithMessage("*allowing 2 retries*");
    }

    private static ServiceProvider Build(string? secretKey)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Payments:Stripe:Provider"] = "stripe",
            ["Payments:Stripe:ApiBase"] = "https://stripe.test",
        };

        if (secretKey is not null)
        {
            settings["Payments:Stripe:SecretKey"] = secretKey;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return Build(settings, new PlatformResiliencePolicyContributor());
    }

    /// <summary>
    /// <b>The adapter requires the platform's resilience services, and that is new.</b> Every
    /// Stripe client is routed through <c>IResilienceExecutor</c> under
    /// <c>financial.payment-provider.stripe</c>, so creating one resolves the executor and the policy
    /// catalogue. Before BARCH-007 these clients needed nothing but a timeout; a deployment that
    /// registers the adapter without <c>AddProductionPlatform</c> now fails when the first client
    /// is created rather than when the first charge is refused, which is the better of the two.
    /// </summary>
    private static ServiceProvider Build(
        Dictionary<string, string?> settings, IOperationalPolicyContributor policies)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock, FixedClock>();
        services.AddSingleton<IOperationalPolicyCatalog>(new OperationalPolicyCatalog([policies]));
        services.AddSingleton<IResilienceExecutor, PassThroughExecutor>();
        services.AddStripePayments(configuration);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Runs the operation. The executor's own behaviour is tested where it lives; what these
    /// tests need is a registration that resolves.
    /// </summary>
    private sealed class PassThroughExecutor : IResilienceExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(
            string dependency,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public Task ExecuteAsync(
            string dependency,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }

    /// <summary>A catalogue that declares a payment retry policy permitting a retry.</summary>
    private sealed class RetryingPaymentPolicyContributor : IOperationalPolicyContributor
    {
        public string Module => "Financial";

        public IEnumerable<OperationalPolicy> Policies =>
        [
            new RetryPolicy(OperationalPolicyNames.CircuitBreaker.StripePayments)
            {
                MaxAttempts = 2
            }
        ];
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;

        public DateTimeOffset Now(string timeZoneId) => DateTimeOffset.UnixEpoch;
    }
}
