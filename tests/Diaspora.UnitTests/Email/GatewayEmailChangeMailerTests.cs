using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using Diaspora.Email;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Email;

/// <summary>
/// What the email-change emails actually carry — 20 Oct 2026. The link goes only in the message to
/// the new address and points at the API's button page; the notices to the old address carry the
/// masked address and no link at all.
/// </summary>
public sealed class GatewayEmailChangeMailerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_link_goes_to_the_change_page_and_says_how_long_it_lasts()
    {
        var gateway = new CapturingGateway();

        var accepted = await Mailer(gateway).SendConfirmationAsync(new EmailChangeLinkMessage
        {
            Address = "new@example.com",
            Token = "tok+en/=",
            ExpiresAt = Now.AddHours(2),
            Language = "en"
        });

        accepted.Should().BeTrue();
        gateway.Sent!.Recipients.Should().Equal("new@example.com");
        gateway.Sent.TextBody.Should().Contain("https://diaspora-api.fly.dev/email/change?token=tok%2Ben%2F%3D");
        gateway.Sent.TextBody.Should().Contain("2 hours");
        gateway.Sent.UnsubscribeUrl.Should().BeNull();
    }

    [Theory]
    [InlineData(EmailChangeNoticeStage.Requested)]
    [InlineData(EmailChangeNoticeStage.Completed)]
    public async Task A_notice_to_the_old_address_carries_the_masked_address_and_no_link(EmailChangeNoticeStage stage)
    {
        var gateway = new CapturingGateway();

        await Mailer(gateway).SendNoticeAsync(new EmailChangeNotice
        {
            Address = "old@example.com",
            MaskedNewAddress = "n**@e******.com",
            Stage = stage,
            LinkExpiresAt = Now.AddHours(2),
            Language = "en"
        });

        gateway.Sent!.Recipients.Should().Equal("old@example.com");
        gateway.Sent.TextBody.Should().Contain("n**@e******.com");
        gateway.Sent.TextBody.Should().NotContain("/email/change").And.NotContain("token=");
    }

    [Fact]
    public async Task An_unconfigured_deployment_sends_nothing()
    {
        var gateway = new CapturingGateway();
        var mailer = new GatewayEmailChangeMailer(
            gateway,
            new StaticOptions(new EmailOptions()),
            new PassThroughResilience(),
            new CatalogueMessages(),
            new FakeClock(Now),
            NullLogger<GatewayEmailChangeMailer>.Instance);

        mailer.IsConfigured.Should().BeFalse();
        (await mailer.SendAddressUnavailableAsync("new@example.com", "en")).Should().BeFalse();
        gateway.Sent.Should().BeNull();
    }

    private static GatewayEmailChangeMailer Mailer(CapturingGateway gateway) =>
        new(
            gateway,
            new StaticOptions(new EmailOptions
            {
                AppBaseUrl = "https://diaspora-api.fly.dev",
                From = new EmailSenderIdentity { Address = "hello@diaspora.test", Name = "Diaspora" }
            }),
            new PassThroughResilience(),
            new CatalogueMessages(),
            new FakeClock(Now),
            NullLogger<GatewayEmailChangeMailer>.Instance);

    private sealed class CapturingGateway : IEmailGateway
    {
        public OutboundEmail? Sent { get; private set; }

        public string Provider => "test";

        public bool IsConfigured => true;

        public Task<EmailDispatch> SendAsync(OutboundEmail message, CancellationToken cancellationToken = default)
        {
            Sent = message;
            return Task.FromResult(new EmailDispatch("test", "id-1"));
        }
    }

    private sealed class PassThroughResilience : IResilienceExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(
            string dependency, Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public Task ExecuteAsync(
            string dependency, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }

    private sealed class StaticOptions(EmailOptions value) : IOptionsMonitor<EmailOptions>
    {
        public EmailOptions CurrentValue { get; } = value;

        public EmailOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<EmailOptions, string?> listener) => null;
    }

    private sealed class CatalogueMessages : ILocalizedMessageProvider
    {
        public string GetMessage(string key) => IdentityMessageCatalog.Resolve(key, "en");

        public string GetMessage(string key, params object[] arguments) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, GetMessage(key), arguments);

        public string GetFor(string key, string? language) => IdentityMessageCatalog.Resolve(key, language);
    }
}
