using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Application.Identity.Validation;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Operations.Policies;
using Diaspora.Operations.Resilience;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Sms;
using Diaspora.Sms.Gateways;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Sms;

/// <summary>
/// The adapter against a real provider account.
/// <para>
/// Skipped unless <c>AT_API_KEY</c> is in the environment, so it is inert in CI and on any machine
/// without credentials. Run it deliberately:
/// </para>
/// <code>
/// AT_USERNAME=sandbox AT_API_KEY=atsk_... SMS_TO=+2348100200001 \
///   dotnet test --filter FullyQualifiedName~LiveSandboxSmsTests
/// </code>
/// <para>
/// <b>Why this exists when a probe script already proves the account.</b> The probe
/// (<c>runtime-validation/probe-sms-provider.mjs</c>) sends its own hand-built request; passing it
/// means the credentials and the destination are good. It says nothing about whether
/// <see cref="AfricasTalkingSmsGateway"/> assembles the same request, reads the reply the same way,
/// or survives the resilience executor wrapped around it. Those are the parts that ship.
/// </para>
/// <para>
/// So this drives the production path — real gateway, real router, real policy catalogue, real
/// executor, real message catalogue — and the only thing standing in for anything is the clock.
/// </para>
/// </summary>
public sealed class LiveSandboxSmsTests
{
    [LiveSms]
    public async Task The_adapter_delivers_a_code_through_the_sandbox()
    {
        var destination = Environment.GetEnvironmentVariable("SMS_TO") ?? "+2348100200001";

        var options = new SmsOptions
        {
            DefaultProvider = SmsProviderNames.AfricasTalking,
            AfricasTalking =
            {
                Username = Environment.GetEnvironmentVariable("AT_USERNAME") ?? "sandbox",
                ApiKey = Environment.GetEnvironmentVariable("AT_API_KEY")!,
                SenderId = Environment.GetEnvironmentVariable("AT_SENDER_ID"),
                // Derived from the username rather than asked for separately: the sandbox only
                // answers on the sandbox host and only for the literal username `sandbox`, and
                // setting one without the other gives a 401 that reads like a bad key.
                UseSandbox = (Environment.GetEnvironmentVariable("AT_USERNAME") ?? "sandbox") == "sandbox",
            },
        };

        var monitor = new StaticOptions(options);

        var gateway = new AfricasTalkingSmsGateway(
            monitor,
            new HttpClient { Timeout = TimeSpan.FromSeconds(20) },
            NullLogger<AfricasTalkingSmsGateway>.Instance);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        var executor = new ResilienceExecutor(
            new OperationalPolicyCatalog(
                [new PlatformResiliencePolicyContributor(), new SmsResiliencePolicyContributor()]),
            [new SmsFailureClassifier(SmsDependencies.AfricasTalking)],
            clock,
            NullLogger<ResilienceExecutor>.Instance);

        var service = new RoutedSmsOtpDeliveryService(
            new SmsGatewayRouter([gateway], monitor),
            executor,
            new CatalogueMessages(),
            clock,
            NullLogger<RoutedSmsOtpDeliveryService>.Instance);

        var delivered = await service.SendAsync(new OtpDeliveryRequest
        {
            PhoneNumber = destination,
            // Not six plausible digits. This lands in a simulator somebody may be watching, and an
            // unannounced code that looks real is a bad habit to build into a test.
            Code = "000000",
            Purpose = OtpPurpose.SignIn,
            Language = "en",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
        });

        // True means a provider accepted it — the property the old placeholder faked by returning
        // true with no transport behind it at all.
        delivered.Should().BeTrue(
            "the sandbox should accept the message; a false here means the adapter read the "
            + "reply differently from the probe script, or the executor refused before sending");
    }

    /// <summary>The real catalogue, so the localised body is what actually goes on the wire.</summary>
    private sealed class CatalogueMessages : ILocalizedMessageProvider
    {
        public string GetMessage(string key) => IdentityMessageCatalog.Resolve(key, "en");

        public string GetMessage(string key, params object[] arguments) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, GetMessage(key), arguments);

        public string GetFor(string key, string? language) => IdentityMessageCatalog.Resolve(key, language);
    }

    private sealed class StaticOptions(SmsOptions value) : IOptionsMonitor<SmsOptions>
    {
        public SmsOptions CurrentValue { get; } = value;

        public SmsOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<SmsOptions, string?> listener) => null;
    }
}

/// <summary>
/// A fact that runs only when SMS credentials are present.
/// <para>
/// Skipped rather than absent, so the reason it did not run is visible in the test output. A test
/// that quietly does not exist is indistinguishable from one that passed.
/// </para>
/// </summary>
public sealed class LiveSmsAttribute : FactAttribute
{
    public LiveSmsAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AT_API_KEY")))
        {
            Skip = "Set AT_USERNAME and AT_API_KEY to run this against a live provider account.";
        }
    }
}
