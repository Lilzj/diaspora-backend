using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Payments.Stripe;
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

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock, FixedClock>();
        services.AddStripePayments(configuration);

        return services.BuildServiceProvider();
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;

        public DateTimeOffset Now(string timeZoneId) => DateTimeOffset.UnixEpoch;
    }
}
