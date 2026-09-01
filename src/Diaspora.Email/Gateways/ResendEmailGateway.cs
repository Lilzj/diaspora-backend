using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Email.Gateways;

/// <summary>
/// Resend's HTTP API.
/// </summary>
/// <remarks>
/// <para>
/// Ported from the sibling EduTech platform, which already runs Resend in production, so the
/// domain verification and bounce handling are known quantities rather than new ones.
/// </para>
/// <para>
/// <b>What was changed in the port, and why.</b> EduTech wraps its client in Polly directly —
/// retry, circuit breaker and timeout declared at the <c>AddHttpClient</c> call. This platform
/// does not: retry and breaking are <c>IOperationalPolicyContributor</c> declarations executed by
/// the platform's own executor, so that every outbound dependency reports through one set of
/// metrics and one breaker registry. Bolting Polly on here would give email a second, invisible
/// resilience stack that the operations dashboard cannot see. The policies live in
/// <see cref="EmailResiliencePolicyContributor"/>; this class only classifies.
/// </para>
/// <para>
/// It also drops EduTech's <c>SendBulkAsync</c>. Resend's <c>/emails/batch</c> is a real
/// endpoint, but a partial batch failure has no representation in <c>EmailSendResult</c> — the
/// channel sends one notification at a time and reports one outcome. Adding a bulk path whose
/// failures cannot be reported per-recipient would lose delivery information silently.
/// </para>
/// </remarks>
public sealed class ResendEmailGateway : IEmailGateway
{
    public const string HttpClientName = "email.resend";

    /// <summary>
    /// Resend's payload is snake_case; the response id is not.
    /// </summary>
    /// <remarks>
    /// Declared once and static: a <see cref="JsonSerializerOptions"/> built per call is a
    /// documented allocation and cache-miss cost on a hot path.
    /// </remarks>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly HttpClient _http;
    private readonly ILogger<ResendEmailGateway> _logger;

    public ResendEmailGateway(
        IOptionsMonitor<EmailOptions> options,
        HttpClient http,
        ILogger<ResendEmailGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);

        _options = options;
        _http = http;
        _logger = logger;
    }

    public string Provider => EmailProviderNames.Resend;

    public bool IsConfigured => _options.CurrentValue.Resend.IsConfigured;

    public async Task<EmailDispatch> SendAsync(
        OutboundEmail message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = _options.CurrentValue.Resend;

        if (!settings.IsConfigured)
        {
            throw new EmailPermanentException(
                Provider, $"'{EmailOptions.SectionName}:Resend' is missing ApiKey.");
        }

        if (message.Recipients.Count == 0)
        {
            throw new EmailPermanentException(Provider, "The message has no recipients.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(BuildPayload(message), options: SerializerOptions)
        };

        // Set per request rather than on the client, so a rotated key takes effect on the next
        // send instead of on the next process start. `IOptionsMonitor` already reloads it.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new EmailTransientException(Provider, "Resend could not be reached.", exception);
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new EmailDispatch(Provider, ReadMessageId(payload));
            }

            var detail = ReadError(payload);

            if (IsTransient(response.StatusCode))
            {
                throw new EmailTransientException(
                    Provider, $"Resend answered {(int)response.StatusCode}: {detail}");
            }

            // Logged here because this is where the vendor's own wording exists, and it is the
            // only thing that explains a rejection. The recipient address is deliberately not
            // logged with it — an address and a failure reason together is a record nobody needs
            // in an aggregator.
            _logger.LogWarning(
                "Resend rejected a message with {Status}: {Detail}",
                (int)response.StatusCode, detail);

            throw new EmailPermanentException(
                Provider, $"Resend rejected the message ({(int)response.StatusCode}): {detail}");
        }
    }

    /// <summary>
    /// Which statuses are worth another attempt.
    /// </summary>
    /// <remarks>
    /// Status only, never the vendor's own error string. Resend documents its codes and changes
    /// them; a classifier that has to recognise each one degrades silently to "permanent" as new
    /// ones appear, which turns a retryable blip into a dropped message.
    /// <para>
    /// <b>422 is deliberately permanent.</b> It is what Resend answers for an unverified sending
    /// domain, and retrying it would hide a configuration fault behind what looks like an outage.
    /// </para>
    /// </remarks>
    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static ResendSendRequest BuildPayload(OutboundEmail message) => new()
    {
        // Resend accepts "Name <address>" and bare addresses. The display name is what a
        // recipient's client shows, so it is worth the formatting.
        From = string.IsNullOrWhiteSpace(message.FromName)
            ? message.FromAddress
            : $"{message.FromName} <{message.FromAddress}>",
        To = [.. message.Recipients],
        Subject = message.Subject,
        Html = message.IsHtml ? message.Body : null,
        Text = message.IsHtml ? null : message.Body,
        Headers = message.UnsubscribeUrl is { Length: > 0 } url
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["List-Unsubscribe"] = $"<{url}>",
                // Tells a client it may unsubscribe with a POST and no confirmation page, which
                // is what Gmail and Outlook require of bulk senders.
                ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click"
            }
            : null
    };

    private static string? ReadMessageId(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
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
        if (string.IsNullOrWhiteSpace(payload))
        {
            return "no detail";
        }

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.TryGetProperty("message", out var message)
                && message.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        catch (JsonException)
        {
            // Not JSON — a proxy or a gateway page. The raw body is still the best evidence.
        }

        // Bounded: an HTML error page in a log line helps nobody and costs storage.
        return payload.Length > 300 ? payload[..300] : payload;
    }

    private sealed class ResendSendRequest
    {
        public string From { get; init; } = default!;

        public string[] To { get; init; } = default!;

        public string Subject { get; init; } = default!;

        public string? Html { get; init; }

        public string? Text { get; init; }

        public Dictionary<string, string>? Headers { get; init; }
    }
}
