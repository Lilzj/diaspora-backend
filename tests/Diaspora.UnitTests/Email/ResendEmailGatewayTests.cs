using System.Net;
using System.Text;
using System.Text.Json;
using Diaspora.Email;
using Diaspora.Email.Gateways;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Email;

/// <summary>
/// What Resend's answer actually means.
/// </summary>
/// <remarks>
/// The distinction every test here turns on is transient versus permanent, because that single
/// classification decides whether the platform retries. Getting it wrong in one direction drops
/// mail that would have been accepted on a second attempt; getting it wrong in the other burns a
/// retry budget on a rejection that can never change, and hides a configuration fault behind
/// what looks like an outage.
/// </remarks>
public sealed class ResendEmailGatewayTests
{
    private const string Recipient = "member@diaspora.test";

    [Fact]
    public async Task An_accepted_message_returns_the_provider_id()
    {
        var gateway = Gateway(HttpStatusCode.OK, """{"id":"re_abc123"}""");

        var dispatch = await gateway.SendAsync(Message());

        dispatch.Provider.Should().Be(EmailProviderNames.Resend);
        dispatch.ProviderMessageId.Should().Be("re_abc123");
    }

    [Fact]
    public async Task An_unreadable_receipt_does_not_lose_an_accepted_message()
    {
        // The message was accepted; only the receipt is unparseable. Throwing here would cost the
        // send to save a reconciliation, which is the wrong way round.
        var gateway = Gateway(HttpStatusCode.OK, "not json at all");

        var dispatch = await gateway.SendAsync(Message());

        dispatch.ProviderMessageId.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task A_status_the_provider_may_recover_from_is_transient(HttpStatusCode status)
    {
        var gateway = Gateway(status, """{"message":"try later"}""");

        var send = async () => await gateway.SendAsync(Message());

        await send.Should().ThrowAsync<EmailTransientException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task A_status_the_provider_will_never_change_its_mind_about_is_permanent(
        HttpStatusCode status)
    {
        var gateway = Gateway(status, """{"message":"no"}""");

        var send = async () => await gateway.SendAsync(Message());

        await send.Should().ThrowAsync<EmailPermanentException>();
    }

    [Fact]
    public async Task An_unverified_sending_domain_is_permanent_rather_than_retried()
    {
        // 422 is what Resend answers when the `from` domain has not been verified. Retrying it
        // would spend the whole budget on a configuration fault and report it as an outage — the
        // operator would look at the provider's status page rather than at their own DNS.
        var gateway = Gateway(
            HttpStatusCode.UnprocessableEntity,
            """{"message":"The diaspora.app domain is not verified."}""");

        var send = async () => await gateway.SendAsync(Message());

        var thrown = await send.Should().ThrowAsync<EmailPermanentException>();
        thrown.Which.Message.Should().Contain("not verified");
    }

    [Fact]
    public async Task An_unreachable_provider_is_transient()
    {
        var gateway = new ResendEmailGateway(
            Options(),
            new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("https://api.resend.com/") },
            NullLogger<ResendEmailGateway>.Instance);

        var send = async () => await gateway.SendAsync(Message());

        await send.Should().ThrowAsync<EmailTransientException>();
    }

    [Fact]
    public async Task A_missing_api_key_is_permanent_and_names_the_configuration_path()
    {
        // Not transient. A missing key is not a blip, and retrying it would have a queue grind
        // through a configuration fault until every message expired.
        var gateway = new ResendEmailGateway(
            Options(apiKey: string.Empty),
            new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")),
            NullLogger<ResendEmailGateway>.Instance);

        var send = async () => await gateway.SendAsync(Message());

        var thrown = await send.Should().ThrowAsync<EmailPermanentException>();
        thrown.Which.Message.Should().Contain("Email:Resend");
    }

    [Fact]
    public async Task A_message_with_no_recipients_is_refused_before_it_costs_a_request()
    {
        var gateway = Gateway(HttpStatusCode.OK, """{"id":"re_1"}""");

        var send = async () => await gateway.SendAsync(Message() with { Recipients = [] });

        await send.Should().ThrowAsync<EmailPermanentException>();
    }

    // ── The payload ─────────────────────────────────────────────────────────

    [Fact]
    public async Task An_html_body_is_sent_as_html_and_a_text_body_as_text()
    {
        // A plain-text body delivered in the `html` field renders as one unbroken paragraph,
        // because the newlines a text body relies on mean nothing in HTML.
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"re_1"}""");
        var gateway = GatewayWith(handler);

        await gateway.SendAsync(Message() with { IsHtml = true, Body = "<p>Hello</p>" });
        var asHtml = JsonDocument.Parse(handler.LastBody).RootElement;

        asHtml.TryGetProperty("html", out _).Should().BeTrue();
        asHtml.TryGetProperty("text", out _).Should().BeFalse();

        await gateway.SendAsync(Message() with { IsHtml = false, Body = "Hello" });
        var asText = JsonDocument.Parse(handler.LastBody).RootElement;

        asText.TryGetProperty("text", out _).Should().BeTrue();
        asText.TryGetProperty("html", out _).Should().BeFalse();
    }

    [Fact]
    public async Task The_sender_is_formatted_so_a_mail_client_shows_the_name()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"re_1"}""");

        await GatewayWith(handler).SendAsync(Message());

        JsonDocument.Parse(handler.LastBody).RootElement
            .GetProperty("from").GetString()
            .Should().Be("Diaspora <no-reply@diaspora.test>");
    }

    [Fact]
    public async Task An_unsubscribe_url_becomes_the_headers_a_mail_client_reads()
    {
        // Gmail and Outlook both require these of bulk senders. As body text they do nothing.
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"re_1"}""");

        await GatewayWith(handler).SendAsync(
            Message() with { UnsubscribeUrl = "https://diaspora.app/unsubscribe/tok" });

        var headers = JsonDocument.Parse(handler.LastBody).RootElement.GetProperty("headers");

        headers.GetProperty("List-Unsubscribe").GetString()
            .Should().Be("<https://diaspora.app/unsubscribe/tok>");
        headers.GetProperty("List-Unsubscribe-Post").GetString()
            .Should().Be("List-Unsubscribe=One-Click");
    }

    [Fact]
    public async Task A_message_with_no_token_carries_no_unsubscribe_headers()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"re_1"}""");

        await GatewayWith(handler).SendAsync(Message());

        JsonDocument.Parse(handler.LastBody).RootElement
            .TryGetProperty("headers", out _).Should().BeFalse();
    }

    [Fact]
    public void A_gateway_without_an_api_key_reports_itself_unconfigured()
    {
        // `EmailNotificationChannel` reads this to decide whether the channel is available at
        // all. A gateway that claimed to be configured would have the platform queue mail it
        // cannot send.
        new ResendEmailGateway(
                Options(apiKey: string.Empty),
                new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")),
                NullLogger<ResendEmailGateway>.Instance)
            .IsConfigured.Should().BeFalse();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static OutboundEmail Message() => new()
    {
        Recipients = [Recipient],
        Subject = "A subject",
        Body = "A body",
        IsHtml = false,
        FromAddress = "no-reply@diaspora.test",
        FromName = "Diaspora"
    };

    private static StaticOptionsMonitor Options(string apiKey = "re_test_key") =>
        new StaticOptionsMonitor(new EmailOptions
        {
            DefaultProvider = EmailProviderNames.Resend,
            From = new EmailSenderIdentity { Address = "no-reply@diaspora.test", Name = "Diaspora" },
            Resend = new ResendOptions { ApiKey = apiKey }
        });

    private static ResendEmailGateway Gateway(HttpStatusCode status, string body) =>
        GatewayWith(new StubHandler(status, body));

    private static ResendEmailGateway GatewayWith(StubHandler handler) =>
        new(
            Options(),
            new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") },
            NullLogger<ResendEmailGateway>.Instance);

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("the network is gone");
    }

    private sealed class StaticOptionsMonitor(EmailOptions value) : IOptionsMonitor<EmailOptions>
    {
        public EmailOptions CurrentValue { get; } = value;

        public EmailOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<EmailOptions, string?> listener) => null;
    }
}
