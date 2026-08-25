using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Sms.Gateways;

/// <summary>
/// Africa's Talking.
/// <para>
/// The cheap half of the routing table, and only for the destinations a deployment's account
/// actually covers — which is why <see cref="SmsOptions.Routes"/> ships empty and the country
/// list is the operator's assertion rather than this repository's.
/// </para>
/// <para>
/// <b>The trap this gateway exists to handle.</b> A rejected message still answers HTTP 201. The
/// per-recipient <c>statusCode</c> inside the body is where acceptance actually lives, so a
/// gateway that checked <c>IsSuccessStatusCode</c> and returned — the obvious implementation, and
/// the one an SDK-shaped example encourages — would report every invalid number, blacklisted
/// recipient and empty balance as a delivered code. The member waits for an SMS that was never
/// carried, and the platform's own logs say it was sent.
/// </para>
/// </summary>
public sealed class AfricasTalkingSmsGateway : ISmsGateway
{
    public const string HttpClientName = "sms.africas-talking";

    private const string LiveEndpoint = "https://api.africastalking.com/version1/messaging";
    private const string SandboxEndpoint = "https://api.sandbox.africastalking.com/version1/messaging";

    private readonly IOptionsMonitor<SmsOptions> _options;
    private readonly HttpClient _http;
    private readonly ILogger<AfricasTalkingSmsGateway> _logger;

    public AfricasTalkingSmsGateway(
        IOptionsMonitor<SmsOptions> options,
        HttpClient http,
        ILogger<AfricasTalkingSmsGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);

        _options = options;
        _http = http;
        _logger = logger;
    }

    public string Provider => SmsProviderNames.AfricasTalking;

    public bool IsConfigured => _options.CurrentValue.AfricasTalking.IsConfigured;

    public async Task<SmsDispatch> SendAsync(
        SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = _options.CurrentValue.AfricasTalking;

        if (!settings.IsConfigured)
        {
            throw new SmsPermanentException(
                Provider,
                $"'{SmsOptions.SectionName}:AfricasTalking' is missing Username or ApiKey.");
        }

        var fields = new List<KeyValuePair<string, string>>
        {
            new("username", settings.Username),
            new("to", message.Destination),
            new("message", message.Body)
        };

        if (!string.IsNullOrWhiteSpace(settings.SenderId))
        {
            fields.Add(new KeyValuePair<string, string>("from", settings.SenderId));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            settings.UseSandbox ? SandboxEndpoint : LiveEndpoint)
        {
            Content = new FormUrlEncodedContent(fields)
        };

        // Their own header, not an Authorization scheme. Sending it as a bearer token
        // authenticates as nobody and answers 401, which looks exactly like a wrong key.
        request.Headers.Add("apiKey", settings.ApiKey);
        request.Headers.Add("Accept", "application/json");

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new SmsTransientException(
                Provider, "The SMS provider could not be reached.", exception);
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if (IsTransient(response.StatusCode))
                {
                    throw new SmsTransientException(
                        Provider,
                        $"The provider answered {(int)response.StatusCode}: {Truncate(payload)}");
                }

                throw new SmsPermanentException(
                    Provider,
                    $"The provider rejected the request ({(int)response.StatusCode}): {Truncate(payload)}");
            }

            return ReadRecipient(payload);
        }
    }

    /// <summary>
    /// Reads the one recipient this platform ever sends to, and decides from its status code.
    /// </summary>
    private SmsDispatch ReadRecipient(string payload)
    {
        JsonElement recipients;

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("SMSMessageData", out var data)
                || !data.TryGetProperty("Recipients", out recipients)
                || recipients.ValueKind is not JsonValueKind.Array)
            {
                throw new SmsTransientException(
                    Provider,
                    $"The provider answered a body with no recipients: {Truncate(payload)}");
            }

            // An accepted request with an empty recipient list is the answer to a destination the
            // account cannot carry — a country not enabled on it, most often. It is a 201 that
            // delivered nothing, and it is transient in shape only.
            if (recipients.GetArrayLength() == 0)
            {
                throw new SmsPermanentException(
                    Provider,
                    "The provider accepted the request but listed no recipients. The destination is "
                    + "most likely a country this account does not carry.");
            }

            var recipient = recipients[0];

            var statusCode = recipient.TryGetProperty("statusCode", out var code)
                && code.ValueKind is JsonValueKind.Number
                    ? code.GetInt32()
                    : 0;

            var status = recipient.TryGetProperty("status", out var text) ? text.GetString() : null;
            var messageId = recipient.TryGetProperty("messageId", out var id) ? id.GetString() : null;

            return Classify(statusCode, status, messageId);
        }
        catch (JsonException exception)
        {
            throw new SmsTransientException(
                Provider,
                $"The provider answered something that is not JSON: {Truncate(payload)}",
                exception);
        }
    }

    /// <summary>
    /// The provider's status codes, grouped by what a caller should do about them.
    /// <para>
    /// 100 Processed, 101 Sent and 102 Queued are all acceptance — queued especially, because
    /// treating it as a failure would resend every message the gateway had already taken.
    /// </para>
    /// </summary>
    private SmsDispatch Classify(int statusCode, string? status, string? messageId)
    {
        var described = status is null
            ? statusCode.ToString(CultureInfo.InvariantCulture)
            : $"{status} ({statusCode})";

        switch (statusCode)
        {
            case 100: // Processed
            case 101: // Sent
            case 102: // Queued
                return new SmsDispatch(Provider, messageId);

            case >= 500: // InternalServerError, GatewayError, RejectedByGateway
                throw new SmsTransientException(
                    Provider, $"The provider did not accept the message: {described}");

            default:
                // 401 RiskHold, 402 InvalidSenderId, 403 InvalidPhoneNumber,
                // 404 UnsupportedNumberType, 405 InsufficientBalance, 406 UserInBlacklist,
                // 407 CouldNotRoute — none of which a second attempt changes.
                _logger.LogWarning("The SMS provider refused a message: {Status}", described);

                throw new SmsPermanentException(
                    Provider, $"The provider refused the message: {described}");
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static string Truncate(string payload) =>
        payload.Length <= 200 ? payload : payload[..200];
}
