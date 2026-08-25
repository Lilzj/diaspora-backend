using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Application.Identity.Validation;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Operations.Policies;
using Diaspora.Operations.Resilience;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Sms;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Sms;

/// <summary>
/// Delivering a one-time code, and what happens when a wire will not carry it.
/// <para>
/// These run against the <em>real</em> <see cref="ResilienceExecutor"/> and the real policy
/// catalogue rather than a stub, because the behaviour under test is the interaction between
/// three things that are each correct alone: the gateway deciding transient or permanent, the
/// classifier translating that for the executor, and the executor deciding whether to retry. A
/// stubbed executor would assert that this class calls something, which is not the property
/// anybody cares about.
/// </para>
/// </summary>
public sealed class SmsOtpDeliveryTests
{
    private const string Nigerian = "+2348100200001";

    [Fact]
    public async Task A_deployment_with_no_provider_reports_failure_rather_than_success()
    {
        // ── The defect this whole adapter exists to end ──
        //
        // The placeholder this replaces returned true unconditionally and logged "Dispatched a
        // code". Nothing was dispatched — there was no transport at all. On the hosted deployment
        // that meant nobody could sign in through the app while the logs said delivery worked.
        var delivered = await Deliver(
            Options(SmsProviderNames.Twilio),
            twilio: null,
            africasTalking: null);

        delivered.Should().BeFalse();
    }

    [Fact]
    public async Task An_accepted_message_reports_success()
    {
        var twilio = Gateway(SmsProviderNames.Twilio);

        var delivered = await Deliver(Options(SmsProviderNames.Twilio), twilio: twilio);

        delivered.Should().BeTrue();
        twilio.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task A_transient_failure_is_retried_on_the_same_wire_before_anything_else()
    {
        // Two attempts, not two providers. Switching wires on the first blip would pay the
        // expensive provider for a fault the cheap one recovers from inside a second.
        var africasTalking = Gateway(
            SmsProviderNames.AfricasTalking,
            failuresBeforeSuccess: 1,
            transient: true);

        var twilio = Gateway(SmsProviderNames.Twilio);

        var delivered = await Deliver(
            Options(SmsProviderNames.Twilio, Route(SmsProviderNames.AfricasTalking, "+234")),
            twilio: twilio,
            africasTalking: africasTalking);

        delivered.Should().BeTrue();
        africasTalking.Attempts.Should().Be(2);
        twilio.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task A_wire_that_stays_down_hands_the_message_to_the_other_one()
    {
        // The entire point of configuring two providers. The regional one is cheaper and it is
        // also one more thing that can be having a bad afternoon.
        var africasTalking = Gateway(
            SmsProviderNames.AfricasTalking,
            failuresBeforeSuccess: int.MaxValue,
            transient: true);

        var twilio = Gateway(SmsProviderNames.Twilio);

        var delivered = await Deliver(
            Options(SmsProviderNames.Twilio, Route(SmsProviderNames.AfricasTalking, "+234")),
            twilio: twilio,
            africasTalking: africasTalking);

        delivered.Should().BeTrue();
        twilio.Attempts.Should().Be(1);

        // Three: the first attempt plus the two retries the policy allows.
        africasTalking.Attempts.Should().Be(3);
    }

    [Fact]
    public async Task A_number_one_provider_rejects_outright_is_not_offered_to_the_other()
    {
        // An unroutable number is unroutable on both wires. Paying a second provider to confirm
        // it is waste, and the member needs to be told their number is wrong rather than routed
        // around until every gateway has refused it.
        var africasTalking = Gateway(
            SmsProviderNames.AfricasTalking,
            failuresBeforeSuccess: int.MaxValue,
            transient: false);

        var twilio = Gateway(SmsProviderNames.Twilio);

        var delivered = await Deliver(
            Options(SmsProviderNames.Twilio, Route(SmsProviderNames.AfricasTalking, "+234")),
            twilio: twilio,
            africasTalking: africasTalking);

        delivered.Should().BeFalse();
        twilio.Attempts.Should().Be(0);

        // One. A permanent failure is not retried either — that is the classifier's job, and this
        // asserts it is actually wired up rather than merely written.
        africasTalking.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task The_message_carries_the_code_and_never_the_whole_challenge()
    {
        var twilio = Gateway(SmsProviderNames.Twilio);

        await Deliver(Options(SmsProviderNames.Twilio), twilio: twilio);

        twilio.LastMessage!.Body.Should().Contain("482913");
        twilio.LastMessage.Destination.Should().Be(Nigerian);
    }

    // ── Fixtures ────────────────────────────────────────────────────────────

    private static SmsRouteOptions Route(string provider, params string[] codes) =>
        new() { Provider = provider, CallingCodes = [.. codes] };

    private static SmsOptions Options(string defaultProvider, params SmsRouteOptions[] routes) =>
        new() { DefaultProvider = defaultProvider, Routes = [.. routes] };

    private static async Task<bool> Deliver(
        SmsOptions options,
        RecordingGateway? twilio = null,
        RecordingGateway? africasTalking = null)
    {
        var gateways = new List<ISmsGateway>();
        if (twilio is not null) gateways.Add(twilio);
        if (africasTalking is not null) gateways.Add(africasTalking);

        var monitor = new StaticOptions(options);
        var router = new SmsGatewayRouter(gateways, monitor);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);

        // The real catalogue: the platform's defaults plus the SMS policies, exactly as the host
        // composes them. A hand-built policy here would let the shipped numbers drift untested.
        var catalog = new OperationalPolicyCatalog(
            [new PlatformResiliencePolicyContributor(), new SmsResiliencePolicyContributor()]);

        var executor = new ResilienceExecutor(
            catalog,
            [
                new SmsFailureClassifier(SmsDependencies.Twilio),
                new SmsFailureClassifier(SmsDependencies.AfricasTalking)
            ],
            clock,
            NullLogger<ResilienceExecutor>.Instance);

        var messages = Substitute.For<ILocalizedMessageProvider>();
        messages.GetFor(IdentityMessageKeys.OtpSmsBody, Arg.Any<string?>())
            .Returns("Your Diaspora code is {0}. It expires in {1} minutes.");

        var service = new RoutedSmsOtpDeliveryService(
            router, executor, messages, clock, NullLogger<RoutedSmsOtpDeliveryService>.Instance);

        return await service.SendAsync(new OtpDeliveryRequest
        {
            PhoneNumber = Nigerian,
            Code = "482913",
            Purpose = OtpPurpose.SignIn,
            Language = "en",
            ExpiresAt = DateTimeOffset.UnixEpoch.AddMinutes(10)
        });
    }

    private static RecordingGateway Gateway(
        string provider,
        int failuresBeforeSuccess = 0,
        bool transient = true) =>
        new(provider, failuresBeforeSuccess, transient);

    /// <summary>A gateway that counts what was asked of it and can be told to fail.</summary>
    private sealed class RecordingGateway(string provider, int failuresBeforeSuccess, bool transient)
        : ISmsGateway
    {
        public string Provider { get; } = provider;

        public bool IsConfigured => true;

        public int Attempts { get; private set; }

        public SmsMessage? LastMessage { get; private set; }

        public Task<SmsDispatch> SendAsync(
            SmsMessage message, CancellationToken cancellationToken = default)
        {
            Attempts++;
            LastMessage = message;

            if (Attempts <= failuresBeforeSuccess)
            {
                return transient
                    ? throw new SmsTransientException(Provider, "temporarily unavailable")
                    : throw new SmsPermanentException(Provider, "the number is not routable");
            }

            return Task.FromResult(new SmsDispatch(Provider, "id"));
        }
    }

    private sealed class StaticOptions(SmsOptions value) : IOptionsMonitor<SmsOptions>
    {
        public SmsOptions CurrentValue { get; } = value;

        public SmsOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<SmsOptions, string?> listener) => null;
    }
}
