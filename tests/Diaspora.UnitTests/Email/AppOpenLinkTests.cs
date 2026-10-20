using Diaspora.Email;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Platform.Operations.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Email;

/// <summary>
/// The address behind an email's "Open in Diaspora" button.
/// <para>
/// Until 20 Oct 2026 it was <c>{AppBaseUrl}{route}</c>, a path the API serves nothing at, so every
/// tap on a phone was a 404. It is now <c>{AppBaseUrl}/open/{route}</c>, which the API answers with
/// a hand-off page and the Android app claims as an App Link.
/// </para>
/// </summary>
public sealed class AppOpenLinkTests
{
    [Theory]
    [InlineData("https://diaspora-api.fly.dev", "/profile/trust-center", "https://diaspora-api.fly.dev/open/profile/trust-center")]
    [InlineData("https://diaspora-api.fly.dev/", "/profile/trust-center", "https://diaspora-api.fly.dev/open/profile/trust-center")]
    [InlineData("https://diaspora-api.fly.dev", "profile/trust-center", "https://diaspora-api.fly.dev/open/profile/trust-center")]
    [InlineData("https://diaspora-api.fly.dev", "/housing/123?tab=viewings", "https://diaspora-api.fly.dev/open/housing/123?tab=viewings")]
    public void An_in_app_route_is_wrapped_under_open(string baseUrl, string route, string expected) =>
        AppOpenLink.Build(baseUrl, route).Should().Be(expected);

    [Theory]
    [InlineData(null, "/profile")]
    [InlineData("", "/profile")]
    [InlineData("https://diaspora-api.fly.dev", null)]
    [InlineData("https://diaspora-api.fly.dev", "")]
    [InlineData("https://diaspora-api.fly.dev", "/")]
    public void Nothing_to_open_means_no_button(string? baseUrl, string? route) =>
        AppOpenLink.Build(baseUrl, route).Should().BeNull();

    [Fact]
    public async Task The_sender_puts_the_open_link_behind_the_button()
    {
        var gateway = new CapturingGateway();
        var sender = new GatewayEmailSender(
            gateway,
            new StaticOptions(new EmailOptions
            {
                AppBaseUrl = "https://diaspora-api.fly.dev",
                From = new EmailSenderIdentity { Address = "hello@diaspora.test", Name = "Diaspora" }
            }),
            new PassThroughResilience(),
            NullLogger<GatewayEmailSender>.Instance);

        var result = await sender.SendAsync(new EmailMessage
        {
            Recipients = [new ChannelEndpoint { Address = "member@diaspora.test" }],
            Subject = "Your verification was approved",
            Body = "Open the trust centre to see it.",
            Format = "text",
            Language = "en",
            Route = "/profile/trust-center",
            NotificationId = Guid.NewGuid(),
            CategoryKey = "trust"
        });

        result.Succeeded.Should().BeTrue();
        gateway.Sent.Should().NotBeNull();
        gateway.Sent!.HtmlBody.Should().Contain("https://diaspora-api.fly.dev/open/profile/trust-center");
        gateway.Sent.TextBody.Should().Contain("https://diaspora-api.fly.dev/open/profile/trust-center");
        gateway.Sent.HtmlBody.Should().NotContain("fly.dev/profile/trust-center");
    }

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
}
