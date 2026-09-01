using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Email;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Email;

/// <summary>
/// What the adapter does — and deliberately does not do — to the container.
/// </summary>
public sealed class EmailAdapterRegistrationTests
{
    [Fact]
    public void With_no_credentials_the_placeholder_is_left_alone()
    {
        // ── Why this matters more than it looks ───────────────────────────────
        //
        // `EmailNotificationChannel.IsAvailable` reads `IEmailSender.IsConfigured`. The
        // placeholder answers false, so an unconfigured deployment routes *around* email. If this
        // adapter registered a credential-less gateway, the channel would flip to "available" and
        // the platform would start queueing mail that can never leave — turning a deliberate
        // absence into a stream of delivery failures.
        var services = new ServiceCollection();

        services.AddEmailAdapter(Configuration());

        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IEmailSender));
    }

    [Fact]
    public void With_an_api_key_the_sender_is_registered()
    {
        var services = new ServiceCollection();

        services.AddEmailAdapter(Configuration(("Email:Resend:ApiKey", "re_test_key")));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEmailSender)
            && descriptor.ImplementationType == typeof(GatewayEmailSender));
    }

    [Fact]
    public void The_gateway_and_its_failure_classifier_are_registered_together()
    {
        // A gateway without its classifier runs under the platform's default, which treats every
        // unrecognised exception as permanent — retry silently off rather than loudly missing.
        var services = new ServiceCollection();

        services.AddEmailAdapter(Configuration(("Email:Resend:ApiKey", "re_test_key")));

        services.Should().Contain(d => d.ServiceType == typeof(IEmailGateway));
        services.Should().Contain(d =>
            d.ServiceType == typeof(Diaspora.Platform.Operations.Resilience.IFailureClassifier));
    }

    [Fact]
    public void Registering_twice_does_not_duplicate_the_policy_contributor()
    {
        // `TryAddEnumerable` guards this. Two contributors declaring the same dependency is a
        // policy conflict the kernel reports at startup.
        var services = new ServiceCollection();
        var configuration = Configuration(("Email:Resend:ApiKey", "re_test_key"));

        services.AddEmailAdapter(configuration);
        services.AddEmailAdapter(configuration);

        services.Count(d =>
                d.ImplementationType == typeof(EmailResiliencePolicyContributor))
            .Should().Be(1);
    }

    [Fact]
    public void A_configured_adapter_validates_its_options_at_startup()
    {
        // The point of validating here rather than at first send: a misconfiguration discovered
        // when the queue drains produces an incident hours after the deploy that caused it.
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddEmailAdapter(Configuration(
            ("Email:Resend:ApiKey", "re_test_key"),
            ("Email:From:Address", "not-an-address")));

        var read = () => services.BuildServiceProvider()
            .GetRequiredService<IOptions<EmailOptions>>().Value;

        read.Should().Throw<OptionsValidationException>()
            .WithMessage("*From:Address*");
    }

    // -- The OTP decoration ------------------------------------------------

    [Fact]
    public void A_configured_adapter_puts_itself_in_front_of_the_otp_transport()
    {
        var services = new ServiceCollection();
        services.AddScoped<IOtpDeliveryService, StubOtpDelivery>();

        services.AddEmailAdapter(Configuration(("Email:Resend:ApiKey", "re_test_key")));

        services.Last(d => d.ServiceType == typeof(IOtpDeliveryService))
            .ImplementationFactory.Should().NotBeNull(
                "the decorator is registered by factory, and last-registration-wins puts it in "
                + "front of the SMS transport");
    }

    [Fact]
    public void The_transport_it_wraps_is_left_registered()
    {
        // Not a replacement. Email cannot carry a phone-verification code, so the SMS transport
        // has to survive underneath rather than be displaced.
        var services = new ServiceCollection();
        services.AddScoped<IOtpDeliveryService, StubOtpDelivery>();

        services.AddEmailAdapter(Configuration(("Email:Resend:ApiKey", "re_test_key")));

        services.Should().Contain(d => d.ImplementationType == typeof(StubOtpDelivery));
    }

    [Fact]
    public void With_no_credentials_the_otp_transport_is_untouched()
    {
        var services = new ServiceCollection();
        services.AddScoped<IOtpDeliveryService, StubOtpDelivery>();

        services.AddEmailAdapter(Configuration());

        services.Count(d => d.ServiceType == typeof(IOtpDeliveryService)).Should().Be(1);
    }

    [Fact]
    public void The_decorator_never_wraps_itself()
    {
        // Two live gateways for one send, and an SMS fallback two layers down. The guard is
        // cheap; finding this in production would not be.
        var services = new ServiceCollection();
        services.AddScoped<IOtpDeliveryService, StubOtpDelivery>();
        var configuration = Configuration(("Email:Resend:ApiKey", "re_test_key"));

        services.AddEmailAdapter(configuration);
        services.AddEmailAdapter(configuration);

        services.Count(d => d.ServiceType == typeof(IOtpDeliveryService)).Should().Be(2);
    }

    [Fact]
    public void With_no_transport_to_wrap_the_adapter_adds_none()
    {
        // Identity registers a transport before this runs, so an empty collection means
        // composition changed. Inventing one here would give recovery a wire with no fallback.
        var services = new ServiceCollection();

        services.AddEmailAdapter(Configuration(("Email:Resend:ApiKey", "re_test_key")));

        services.Should().NotContain(d => d.ServiceType == typeof(IOtpDeliveryService));
    }

    private sealed class StubOtpDelivery : IOtpDeliveryService
    {
        public Task<bool> SendAsync(
            OtpDeliveryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public OtpDeliveryChannel ChannelFor(OtpPurpose purpose) => OtpDeliveryChannel.Sms;
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair =>
                new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
}
