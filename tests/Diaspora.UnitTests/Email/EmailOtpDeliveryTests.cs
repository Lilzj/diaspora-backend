using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Application.Identity.Validation;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Email;
using Diaspora.Operations.Policies;
using Diaspora.Operations.Resilience;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Email;

/// <summary>
/// Which wire a one-time code goes out on.
/// </summary>
/// <remarks>
/// <para>
/// The decorator exists because email is right for exactly one kind of code and wrong for the
/// rest, so almost every test here is about a purpose being <em>refused</em> rather than carried.
/// Getting that split wrong is not a delivery bug: an emailed phone-verification code verifies
/// nothing, and an emailed second factor is not a second factor.
/// </para>
/// <para>
/// These run against the real <see cref="ResilienceExecutor"/> and the real policy catalogue, so
/// the shipped retry and breaker numbers are the ones under test rather than a hand-built pair.
/// </para>
/// </remarks>
public sealed class EmailOtpDeliveryTests
{
    private const string Mailbox = "member@diaspora.test";

    // ── What email carries ──────────────────────────────────────────────────

    [Fact]
    public async Task A_recovery_code_goes_to_the_mailbox_rather_than_the_phone()
    {
        var gateway = new RecordingGateway();
        var sms = new RecordingInner();

        var delivered = await Service(gateway, sms).SendAsync(Request(OtpPurpose.PasswordReset));

        delivered.Should().BeTrue();
        gateway.Sent.Should().ContainSingle();
        gateway.Sent[0].Recipients.Should().Equal(Mailbox);
        sms.Calls.Should().Be(0);
    }

    [Fact]
    public async Task An_account_recovery_code_goes_the_same_way()
    {
        var gateway = new RecordingGateway();
        var sms = new RecordingInner();

        await Service(gateway, sms).SendAsync(Request(OtpPurpose.AccountRecovery));

        gateway.Sent.Should().ContainSingle();
        sms.Calls.Should().Be(0);
    }

    // ── What it refuses to carry ────────────────────────────────────────────

    [Fact]
    public async Task A_phone_verification_code_is_left_to_the_sms_transport()
    {
        // The code *is* the proof. Emailing it would confirm the member holds a mailbox, which is
        // not the claim being verified, and would mark an unowned number as verified.
        var gateway = new RecordingGateway();
        var sms = new RecordingInner();

        await Service(gateway, sms).SendAsync(Request(OtpPurpose.PhoneVerification));

        gateway.Sent.Should().BeEmpty();
        sms.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(OtpPurpose.StepUpAuthentication)]
    [InlineData(OtpPurpose.SignIn)]
    public async Task A_second_factor_is_left_to_the_sms_transport(OtpPurpose purpose)
    {
        // An emailed code is not a second factor when the mailbox is also where a password reset
        // lands: whoever holds the mailbox holds both halves.
        var gateway = new RecordingGateway();
        var sms = new RecordingInner();

        await Service(gateway, sms).SendAsync(Request(purpose));

        gateway.Sent.Should().BeEmpty();
        sms.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_member_with_no_email_address_still_gets_their_code()
    {
        // Accounts predating the move to email sign-in have no address on file.
        var gateway = new RecordingGateway();
        var sms = new RecordingInner();

        await Service(gateway, sms)
            .SendAsync(Request(OtpPurpose.PasswordReset) with { EmailAddress = null });

        gateway.Sent.Should().BeEmpty();
        sms.Calls.Should().Be(1);
    }

    [Fact]
    public async Task An_unconfigured_gateway_changes_nothing_about_delivery()
    {
        var gateway = new RecordingGateway { IsConfigured = false };
        var sms = new RecordingInner();

        await Service(gateway, sms).SendAsync(Request(OtpPurpose.PasswordReset));

        gateway.Sent.Should().BeEmpty();
        sms.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_missing_sender_address_changes_nothing_about_delivery()
    {
        // Credentials without a verified `from` fail every send. Treating that as "configured"
        // would route recovery into a gateway that refuses it and call the result email.
        var gateway = new RecordingGateway();
        var sms = new RecordingInner();

        await Service(gateway, sms, senderAddress: string.Empty)
            .SendAsync(Request(OtpPurpose.PasswordReset));

        gateway.Sent.Should().BeEmpty();
        sms.Calls.Should().Be(1);
    }

    // ── When email is configured but fails ──────────────────────────────────

    [Fact]
    public async Task A_refused_email_falls_back_to_sms_so_the_member_is_not_locked_out()
    {
        var gateway = new RecordingGateway
        {
            Fault = () => new EmailPermanentException(EmailProviderNames.Resend, "bad address")
        };
        var sms = new RecordingInner();

        var delivered = await Service(gateway, sms).SendAsync(Request(OtpPurpose.PasswordReset));

        delivered.Should().BeTrue();
        sms.Calls.Should().Be(1);
    }

    [Fact]
    public async Task An_email_outage_falls_back_to_sms()
    {
        var gateway = new RecordingGateway
        {
            Fault = () => new EmailTransientException(EmailProviderNames.Resend, "503")
        };
        var sms = new RecordingInner();

        var delivered = await Service(gateway, sms).SendAsync(Request(OtpPurpose.PasswordReset));

        delivered.Should().BeTrue();
        sms.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_failure_on_both_wires_is_reported_as_a_failure()
    {
        // The member is told the code could not be sent, which is a screen they can act on.
        // Reporting success here would leave them waiting for a message that is never coming.
        var gateway = new RecordingGateway
        {
            Fault = () => new EmailTransientException(EmailProviderNames.Resend, "503")
        };
        var sms = new RecordingInner { Result = false };

        var delivered = await Service(gateway, sms).SendAsync(Request(OtpPurpose.PasswordReset));

        delivered.Should().BeFalse();
    }

    [Fact]
    public async Task A_cancelled_send_does_not_fall_back()
    {
        // Shutdown is not a delivery failure. Falling back here would send an SMS on the way out
        // of the process, to a member who is no longer waiting for one.
        var gateway = new RecordingGateway();
        var sms = new RecordingInner();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var send = async () => await Service(gateway, sms)
            .SendAsync(Request(OtpPurpose.PasswordReset), cancelled.Token);

        await send.Should().ThrowAsync<OperationCanceledException>();
        sms.Calls.Should().Be(0);
    }

    // ── What the member actually receives ───────────────────────────────────

    [Fact]
    public async Task The_email_carries_the_code_and_the_minutes_left()
    {
        var gateway = new RecordingGateway();

        await Service(gateway, new RecordingInner()).SendAsync(Request(OtpPurpose.PasswordReset));

        gateway.Sent[0].Body.Should().Contain("482913").And.Contain("10");
        gateway.Sent[0].Subject.Should().Be("Your Diaspora verification code");
        gateway.Sent[0].IsHtml.Should().BeFalse();
    }

    [Fact]
    public async Task A_code_expiring_within_the_minute_does_not_read_as_already_dead()
    {
        // Rounding down gives "expires in 0 minutes", which reads as a code not worth typing.
        var gateway = new RecordingGateway();

        await Service(gateway, new RecordingInner()).SendAsync(
            Request(OtpPurpose.PasswordReset) with
            {
                ExpiresAt = DateTimeOffset.UnixEpoch.AddSeconds(40)
            });

        gateway.Sent[0].Body.Should().Contain("1 minute");
    }

    [Fact]
    public async Task The_code_is_never_in_the_subject_line()
    {
        // A subject is what a locked phone shows on its own. A recovery code readable without
        // unlocking the device is a weaker code than the one in the body.
        var gateway = new RecordingGateway();

        await Service(gateway, new RecordingInner()).SendAsync(Request(OtpPurpose.PasswordReset));

        gateway.Sent[0].Subject.Should().NotContain("482913");
    }

    [Fact]
    public async Task A_recovery_code_carries_no_unsubscribe_link()
    {
        // Offering to unsubscribe from a code the member asked for seconds ago is offering to
        // opt out of signing in.
        var gateway = new RecordingGateway();

        await Service(gateway, new RecordingInner()).SendAsync(Request(OtpPurpose.PasswordReset));

        gateway.Sent[0].UnsubscribeUrl.Should().BeNull();
    }

    [Fact]
    public async Task A_broken_translation_still_delivers_the_digits()
    {
        var gateway = new RecordingGateway();
        var messages = Substitute.For<ILocalizedMessageProvider>();
        messages.GetFor(IdentityMessageKeys.OtpEmailSubject, Arg.Any<string?>()).Returns("Code");
        messages.GetFor(IdentityMessageKeys.OtpEmailBody, Arg.Any<string?>())
            .Returns("Votre code est {0} et {2} n'existe pas");

        await Service(gateway, new RecordingInner(), messages: messages)
            .SendAsync(Request(OtpPurpose.PasswordReset));

        gateway.Sent[0].Body.Should().Be("482913");
    }

    // ── What recovery is told to say ────────────────────────────────────────

    [Fact]
    public void Recovery_is_told_to_name_email()
    {
        Service(new RecordingGateway(), new RecordingInner())
            .ChannelFor(OtpPurpose.PasswordReset)
            .Should().Be(OtpDeliveryChannel.Email);
    }

    [Fact]
    public void Phone_verification_is_told_to_name_whatever_carries_it()
    {
        Service(new RecordingGateway(), new RecordingInner())
            .ChannelFor(OtpPurpose.PhoneVerification)
            .Should().Be(OtpDeliveryChannel.Sms);
    }

    [Fact]
    public void An_unconfigured_gateway_defers_the_answer_to_the_wrapped_transport()
    {
        var sms = new RecordingInner { Channel = OtpDeliveryChannel.None };

        Service(new RecordingGateway { IsConfigured = false }, sms)
            .ChannelFor(OtpPurpose.PasswordReset)
            .Should().Be(OtpDeliveryChannel.None);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static OtpDeliveryRequest Request(OtpPurpose purpose) => new()
    {
        PhoneNumber = "+2348012345678",
        EmailAddress = Mailbox,
        Code = "482913",
        Purpose = purpose,
        Language = "en",
        ExpiresAt = DateTimeOffset.UnixEpoch.AddMinutes(10)
    };

    private static EmailOtpDeliveryService Service(
        RecordingGateway gateway,
        RecordingInner inner,
        string senderAddress = "no-reply@diaspora.test",
        ILocalizedMessageProvider? messages = null)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);

        // The real catalogue, exactly as the host composes it. A hand-built policy here would let
        // the shipped numbers drift untested.
        var catalog = new OperationalPolicyCatalog(
            [new PlatformResiliencePolicyContributor(), new EmailResiliencePolicyContributor()]);

        var executor = new ResilienceExecutor(
            catalog,
            [new EmailFailureClassifier(EmailDependencies.Resend)],
            clock,
            NullLogger<ResilienceExecutor>.Instance);

        if (messages is null)
        {
            messages = Substitute.For<ILocalizedMessageProvider>();
            messages.GetFor(IdentityMessageKeys.OtpEmailSubject, Arg.Any<string?>())
                .Returns("Your Diaspora verification code");
            messages.GetFor(IdentityMessageKeys.OtpEmailBody, Arg.Any<string?>())
                .Returns("Your Diaspora code is {0}. It expires in {1} minute(s).");
        }

        var options = new EmailOptions
        {
            DefaultProvider = EmailProviderNames.Resend,
            From = new EmailSenderIdentity { Address = senderAddress, Name = "Diaspora" },
            Resend = new ResendOptions { ApiKey = "re_test_key" }
        };

        return new EmailOtpDeliveryService(
            inner,
            gateway,
            new StaticOptions(options),
            executor,
            messages,
            clock,
            NullLogger<EmailOtpDeliveryService>.Instance);
    }

    private sealed class RecordingGateway : IEmailGateway
    {
        public List<OutboundEmail> Sent { get; } = [];

        public string Provider => EmailProviderNames.Resend;

        public bool IsConfigured { get; init; } = true;

        public Func<Exception>? Fault { get; init; }

        public Task<EmailDispatch> SendAsync(
            OutboundEmail message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Fault is not null)
            {
                throw Fault();
            }

            Sent.Add(message);
            return Task.FromResult(new EmailDispatch(Provider, "re_stub"));
        }
    }

    private sealed class RecordingInner : IOtpDeliveryService
    {
        public int Calls { get; private set; }

        public bool Result { get; init; } = true;

        public OtpDeliveryChannel Channel { get; init; } = OtpDeliveryChannel.Sms;

        public Task<bool> SendAsync(
            OtpDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }

        public OtpDeliveryChannel ChannelFor(OtpPurpose purpose) => Channel;
    }

    private sealed class StaticOptions(EmailOptions value) : IOptionsMonitor<EmailOptions>
    {
        public EmailOptions CurrentValue { get; } = value;

        public EmailOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<EmailOptions, string?> listener) => null;
    }
}
