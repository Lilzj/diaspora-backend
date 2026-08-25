using System.Net;
using System.Text;
using Diaspora.Sms;
using Diaspora.Sms.Gateways;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Sms;

/// <summary>
/// What each vendor's answer actually means.
/// <para>
/// The distinction every test here turns on is transient versus permanent, because that single
/// classification decides whether the platform retries. Getting it wrong in one direction drops
/// codes that would have arrived on a second attempt; getting it wrong in the other pays a
/// provider three times to reject the same invalid number.
/// </para>
/// </summary>
public sealed class SmsGatewayTests
{
    private const string Destination = "+2348100200001";

    // ── Africa's Talking ────────────────────────────────────────────────────

    [Fact]
    public async Task A_rejected_recipient_is_a_failure_even_though_the_response_said_201()
    {
        // ── The whole reason this gateway does not trust IsSuccessStatusCode ──
        //
        // A refused message still answers 201 Created. The truth is the per-recipient statusCode
        // inside the body — here 403, InvalidPhoneNumber. The obvious implementation checks the
        // HTTP status and returns, which would report every invalid number, blacklisted recipient
        // and empty balance as a delivered code: the member waits for an SMS that was never
        // carried, and the platform's own logs say it was sent.
        var gateway = AfricasTalking(HttpStatusCode.Created, Recipients(403, "InvalidPhoneNumber"));

        var send = async () => await gateway.SendAsync(new SmsMessage(Destination, "code"));

        await send.Should().ThrowAsync<SmsPermanentException>();
    }

    [Theory]
    [InlineData(100, "Processed")]
    [InlineData(101, "Sent")]
    [InlineData(102, "Queued")]
    public async Task An_accepted_recipient_is_a_success(int statusCode, string status)
    {
        // Queued especially. Treating it as a failure would resend every message the gateway had
        // already taken responsibility for.
        var gateway = AfricasTalking(HttpStatusCode.Created, Recipients(statusCode, status));

        var dispatch = await gateway.SendAsync(new SmsMessage(Destination, "code"));

        dispatch.Provider.Should().Be(SmsProviderNames.AfricasTalking);
        dispatch.ProviderMessageId.Should().Be("ATXid_stub");
    }

    [Theory]
    [InlineData(405, "InsufficientBalance")]
    [InlineData(406, "UserInBlacklist")]
    [InlineData(407, "CouldNotRoute")]
    public async Task A_recipient_the_provider_will_never_carry_is_permanent(int statusCode, string status)
    {
        var gateway = AfricasTalking(HttpStatusCode.Created, Recipients(statusCode, status));

        var send = async () => await gateway.SendAsync(new SmsMessage(Destination, "code"));

        await send.Should().ThrowAsync<SmsPermanentException>();
    }

    [Fact]
    public async Task A_gateway_error_on_a_recipient_is_worth_retrying()
    {
        var gateway = AfricasTalking(HttpStatusCode.Created, Recipients(501, "GatewayError"));

        var send = async () => await gateway.SendAsync(new SmsMessage(Destination, "code"));

        await send.Should().ThrowAsync<SmsTransientException>();
    }

    [Fact]
    public async Task An_accepted_request_with_no_recipients_is_permanent()
    {
        // Their answer to a country the account does not carry: a 201 that delivered nothing.
        // Retrying it would spend the whole budget confirming the account's coverage.
        var gateway = AfricasTalking(
            HttpStatusCode.Created,
            """{"SMSMessageData":{"Message":"Sent to 0/1","Recipients":[]}}""");

        var send = async () => await gateway.SendAsync(new SmsMessage(Destination, "code"));

        await send.Should().ThrowAsync<SmsPermanentException>();
    }

    [Fact]
    public async Task A_bad_api_key_is_permanent()
    {
        // It looks like something that might heal and never does. Retrying it three times per send
        // turns one wrong key into a rate-limit ban and an open circuit.
        var gateway = AfricasTalking(HttpStatusCode.Unauthorized, "Unauthorized");

        var send = async () => await gateway.SendAsync(new SmsMessage(Destination, "code"));

        await send.Should().ThrowAsync<SmsPermanentException>();
    }

    [Fact]
    public async Task An_unconfigured_gateway_refuses_before_it_reaches_the_wire()
    {
        var gateway = new AfricasTalkingSmsGateway(
            new StaticOptions(new SmsOptions()),
            new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")),
            NullLogger<AfricasTalkingSmsGateway>.Instance);

        var send = async () => await gateway.SendAsync(new SmsMessage(Destination, "code"));

        await send.Should().ThrowAsync<SmsPermanentException>();
    }

    // ── Twilio ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Twilio_returns_the_message_sid_on_acceptance()
    {
        var gateway = Twilio(HttpStatusCode.Created, """{"sid":"SM123","status":"queued"}""");

        var dispatch = await gateway.SendAsync(new SmsMessage(Destination, "code"));

        dispatch.Provider.Should().Be(SmsProviderNames.Twilio);
        dispatch.ProviderMessageId.Should().Be("SM123");
    }

    [Fact]
    public async Task A_rate_limit_is_worth_retrying()
    {
        // The shape a burst of sign-ins takes. Classifying it permanent would drop exactly the
        // codes a busy minute generated.
        var gateway = Twilio(HttpStatusCode.TooManyRequests, """{"message":"Too many requests"}""");

        var send = async () => await gateway.SendAsync(new SmsMessage(Destination, "code"));

        await send.Should().ThrowAsync<SmsTransientException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task A_provider_side_fault_is_worth_retrying(HttpStatusCode status)
    {
        var gateway = Twilio(status, "upstream failure");

        var send = async () => await gateway.SendAsync(new SmsMessage(Destination, "code"));

        await send.Should().ThrowAsync<SmsTransientException>();
    }

    [Fact]
    public async Task An_invalid_number_is_permanent()
    {
        var gateway = Twilio(
            HttpStatusCode.BadRequest,
            """{"code":21211,"message":"The 'To' number is not a valid phone number."}""");

        var send = async () => await gateway.SendAsync(new SmsMessage(Destination, "code"));

        await send.Should().ThrowAsync<SmsPermanentException>()
            .WithMessage("*not a valid phone number*");
    }

    [Fact]
    public async Task A_body_that_is_not_json_still_produces_a_usable_message()
    {
        // A provider answering an HTML error page must not turn into a JsonException that
        // classifies as permanent for the wrong reason.
        var gateway = Twilio(HttpStatusCode.BadRequest, "<html><body>Bad Request</body></html>");

        var send = async () => await gateway.SendAsync(new SmsMessage(Destination, "code"));

        await send.Should().ThrowAsync<SmsPermanentException>();
    }

    [Fact]
    public async Task A_messaging_service_sid_is_sent_as_a_messaging_service_not_as_a_from_number()
    {
        // Sending an MG… SID in `From` is accepted by nothing: it fails as an unroutable sender,
        // which reads like a problem with the member's number rather than with the configuration.
        var handler = new StubHandler(HttpStatusCode.Created, """{"sid":"SM1"}""");

        var gateway = new TwilioSmsGateway(
            new StaticOptions(new SmsOptions
            {
                DefaultProvider = SmsProviderNames.Twilio,
                Twilio = { AccountSid = "AC1", AuthToken = "secret", From = "MG9999" }
            }),
            new HttpClient(handler),
            NullLogger<TwilioSmsGateway>.Instance);

        await gateway.SendAsync(new SmsMessage(Destination, "code"));

        handler.LastBody.Should().Contain("MessagingServiceSid=MG9999");
        handler.LastBody.Should().NotContain("From=");
    }

    [Fact]
    public async Task A_plain_number_is_sent_as_a_from_number()
    {
        var handler = new StubHandler(HttpStatusCode.Created, """{"sid":"SM1"}""");

        var gateway = new TwilioSmsGateway(
            new StaticOptions(new SmsOptions
            {
                DefaultProvider = SmsProviderNames.Twilio,
                Twilio = { AccountSid = "AC1", AuthToken = "secret", From = "+441234567890" }
            }),
            new HttpClient(handler),
            NullLogger<TwilioSmsGateway>.Instance);

        await gateway.SendAsync(new SmsMessage(Destination, "code"));

        handler.LastBody.Should().Contain("From=");
        handler.LastBody.Should().NotContain("MessagingServiceSid");
    }

    [Fact]
    public async Task A_transport_failure_is_worth_retrying()
    {
        var gateway = new TwilioSmsGateway(
            new StaticOptions(Configured()),
            new HttpClient(new ThrowingHandler()),
            NullLogger<TwilioSmsGateway>.Instance);

        var send = async () => await gateway.SendAsync(new SmsMessage(Destination, "code"));

        await send.Should().ThrowAsync<SmsTransientException>();
    }

    // ── Fixtures ────────────────────────────────────────────────────────────

    /// <summary>
    /// One recipient, shaped the way the provider actually answers.
    /// <para>
    /// Built by concatenation rather than as a raw literal: the payload ends in three consecutive
    /// closing braces, which an interpolated raw string cannot hold without more dollar signs than
    /// anybody should have to count.
    /// </para>
    /// </summary>
    private static string Recipients(int statusCode, string status) =>
        "{\"SMSMessageData\":{\"Message\":\"Sent to 1/1\",\"Recipients\":["
        + "{\"statusCode\":" + statusCode
        + ",\"number\":\"" + Destination + "\""
        + ",\"status\":\"" + status + "\""
        + ",\"cost\":\"NGN 2.2000\""
        + ",\"messageId\":\"ATXid_stub\"}]}}";

    private static SmsOptions Configured() => new()
    {
        DefaultProvider = SmsProviderNames.Twilio,
        Twilio = { AccountSid = "AC1", AuthToken = "secret", From = "+441234567890" },
        AfricasTalking = { Username = "diaspora", ApiKey = "key" }
    };

    private static AfricasTalkingSmsGateway AfricasTalking(HttpStatusCode status, string body) =>
        new(
            new StaticOptions(Configured()),
            new HttpClient(new StubHandler(status, body)),
            NullLogger<AfricasTalkingSmsGateway>.Instance);

    private static TwilioSmsGateway Twilio(HttpStatusCode status, string body) =>
        new(
            new StaticOptions(Configured()),
            new HttpClient(new StubHandler(status, body)),
            NullLogger<TwilioSmsGateway>.Instance);

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
            throw new HttpRequestException("the socket went away");
    }

    private sealed class StaticOptions(SmsOptions value) : IOptionsMonitor<SmsOptions>
    {
        public SmsOptions CurrentValue { get; } = value;

        public SmsOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<SmsOptions, string?> listener) => null;
    }
}
