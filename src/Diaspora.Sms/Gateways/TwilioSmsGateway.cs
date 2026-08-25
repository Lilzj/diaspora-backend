using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Sms.Gateways;

/// <summary>
/// Twilio Programmable Messaging.
/// <para>
/// The global half of the routing table. It is the more expensive wire almost everywhere the
/// cheaper one reaches, and the only one that reaches the rest of the world — which is the whole
/// basis on which <see cref="SmsGatewayRouter"/> chooses.
/// </para>
/// <para>
/// Called over raw HTTP rather than through the vendor SDK. The API used here is one form-encoded
/// POST; an SDK would add a dependency, its transitive graph and its own retry loop layered under
/// the platform's, and the platform's is the one that is catalogue-driven and visible on a
/// dashboard.
/// </para>
/// </summary>
public sealed class TwilioSmsGateway : ISmsGateway
{
    public const string HttpClientName = "sms.twilio";

    private readonly IOptionsMonitor<SmsOptions> _options;
    private readonly HttpClient _http;
    private readonly ILogger<TwilioSmsGateway> _logger;

    public TwilioSmsGateway(
        IOptionsMonitor<SmsOptions> options,
        HttpClient http,
        ILogger<TwilioSmsGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);

        _options = options;
        _http = http;
        _logger = logger;
    }

    public string Provider => SmsProviderNames.Twilio;

    public bool IsConfigured => _options.CurrentValue.Twilio.IsConfigured;

    public async Task<SmsDispatch> SendAsync(
        SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = _options.CurrentValue.Twilio;

        if (!settings.IsConfigured)
        {
            throw new SmsPermanentException(
                Provider,
                $"'{SmsOptions.SectionName}:Twilio' is missing AccountSid, AuthToken or From.");
        }

        var fields = new List<KeyValuePair<string, string>>
        {
            new("To", message.Destination),
            new("Body", message.Body)
        };

        // A Messaging Service SID and a plain number go in different fields, and sending the SID
        // as `From` is accepted by nothing — it fails as an unroutable sender, which reads like a
        // problem with the member's number rather than with this line.
        fields.Add(settings.From.StartsWith("MG", StringComparison.Ordinal)
            ? new KeyValuePair<string, string>("MessagingServiceSid", settings.From)
            : new KeyValuePair<string, string>("From", settings.From));

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(settings.AccountSid)}/Messages.json")
        {
            Content = new FormUrlEncodedContent(fields)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{settings.AccountSid}:{settings.AuthToken}")));

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new SmsTransientException(Provider, "Twilio could not be reached.", exception);
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new SmsDispatch(Provider, ReadMessageSid(payload));
            }

            var detail = ReadError(payload);

            // Status first, provider code second. The status is the part Twilio guarantees; the
            // numeric code is documentation that changes, and a classifier that depends on
            // recognising every code silently degrades to "permanent" as they add new ones.
            if (IsTransient(response.StatusCode))
            {
                throw new SmsTransientException(
                    Provider, $"Twilio answered {(int)response.StatusCode}: {detail}");
            }

            // Logged here rather than at the router, because this is where the provider's own
            // wording exists and it is the only thing that explains a rejection. The destination
            // is not logged with it — a phone number and a failure reason together is a record
            // nobody needs in an aggregator.
            _logger.LogWarning(
                "Twilio rejected a message with {Status}: {Detail}",
                (int)response.StatusCode, detail);

            throw new SmsPermanentException(
                Provider, $"Twilio rejected the message ({(int)response.StatusCode}): {detail}");
        }
    }

    /// <summary>
    /// Whether Twilio's answer is worth another attempt.
    /// <para>
    /// 429 is the one that matters: it is the shape a burst of sign-ins takes, and treating it as
    /// permanent would drop exactly the codes that a busy minute generated.
    /// </para>
    /// </summary>
    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static string? ReadMessageSid(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty("sid", out var sid)
                ? sid.GetString()
                : null;
        }
        catch (JsonException)
        {
            // The message was accepted; only the receipt is unreadable. Losing the id costs a
            // reconciliation, and throwing here would cost the send.
            return null;
        }
    }

    private static string ReadError(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var text = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            var code = root.TryGetProperty("code", out var c) && c.ValueKind is JsonValueKind.Number
                ? c.GetInt32().ToString(CultureInfo.InvariantCulture)
                : null;

            return (text, code) switch
            {
                (not null, not null) => $"{text} (code {code})",
                (not null, null) => text,
                _ => Truncate(payload)
            };
        }
        catch (JsonException)
        {
            return Truncate(payload);
        }
    }

    /// <summary>A provider that answered HTML has said enough in its first line.</summary>
    private static string Truncate(string payload) =>
        payload.Length <= 200 ? payload : payload[..200];
}
