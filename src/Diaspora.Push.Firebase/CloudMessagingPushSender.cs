using System.Net;
using System.Text;
using System.Text.Json;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Push.Firebase;

/// <summary>
/// Sends push notifications through Firebase Cloud Messaging's HTTP v1 API.
///
/// <para>
/// <b>This is the wire and nothing else.</b> The channel above it decides what a notification
/// becomes, which endpoints to use and what a dead token means; this type turns a
/// <see cref="PushMessage"/> into one HTTP request per endpoint and turns the answers back into a
/// <see cref="PushSendResult"/>. That split is why swapping to APNs replaces this file and leaves
/// the channel, its tests and its analytics alone.
/// </para>
///
/// <para>
/// <b>One request per endpoint, deliberately.</b> FCM v1 removed the multicast send that the
/// legacy API had; the supported way to reach several devices is either a topic or one call each.
/// A topic would be wrong here — these are one member's own devices, not a broadcast audience —
/// so the endpoints are sent individually and the per-endpoint outcomes are folded into one
/// result. Devices are contacted concurrently, bounded, because a member with six devices should
/// not wait six round trips.
/// </para>
///
/// <para>
/// <b>A token is a credential and never appears in a log.</b> Failures are reported by endpoint id
/// — the row that owns the token — which is what the channel needs to deactivate it anyway.
/// </para>
/// </summary>
public sealed class CloudMessagingPushSender : IPushNotificationSender
{
    private readonly IOptionsMonitor<PushProviderOptions> _options;
    private readonly ServiceAccountCredential _credential;
    private readonly HttpClient _http;
    private readonly ILogger<CloudMessagingPushSender> _logger;

    /// <summary>
    /// How many devices are contacted at once. A member's device count is small; the bound exists
    /// so a pathological registration cannot open dozens of sockets at once.
    /// </summary>
    private const int MaxParallelEndpoints = 5;

    public CloudMessagingPushSender(
        IOptionsMonitor<PushProviderOptions> options,
        ServiceAccountCredential credential,
        HttpClient http,
        ILogger<CloudMessagingPushSender> logger)
    {
        _options = options;
        _credential = credential;
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// True whenever this adapter is registered. Registration is the statement that a provider is
    /// wired — the startup validator refuses a named provider with no adapter behind it, so the
    /// two cannot disagree.
    /// </summary>
    public bool IsConfigured => true;

    public async Task<PushSendResult> SendAsync(
        PushMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Endpoints.Count == 0)
        {
            return PushSendResult.Failure(
                "NoEndpoint", "The recipient has no usable push endpoint.", isPermanent: true);
        }

        var endpoint = $"https://fcm.googleapis.com/v1/projects/{_credential.ProjectId}/messages:send";

        string accessToken;
        try
        {
            accessToken = await _credential.GetAccessTokenAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Credential problems are the platform's, not the device's. Transient by default:
            // a rotated key or a clock skew is fixed by an operator, and permanently failing every
            // notification in the meantime would lose them.
            _logger.LogError(exception,
                "Push credential exchange failed for service account {ServiceAccount}.",
                _credential.ClientEmail);

            return PushSendResult.Failure(
                "CredentialUnavailable", "Could not obtain a provider access token.", isPermanent: false);
        }

        var results = new List<EndpointOutcome>(message.Endpoints.Count);

        foreach (var chunk in message.Endpoints.Chunk(MaxParallelEndpoints))
        {
            var outcomes = await Task.WhenAll(chunk.Select(target =>
                SendToEndpointAsync(endpoint, accessToken, target, message, cancellationToken)));

            results.AddRange(outcomes);
        }

        return Fold(results, message);
    }

    private async Task<EndpointOutcome> SendToEndpointAsync(
        string endpoint,
        string accessToken,
        ChannelEndpoint target,
        PushMessage message,
        CancellationToken cancellationToken)
    {
        var payload = BuildPayload(target, message);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new("Bearer", accessToken);

            using var response = await _http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new EndpointOutcome(target.EndpointId, Delivered: true, IsGone: false, Detail: ReadMessageName(body));
            }

            // 404 UNREGISTERED and 400 with an invalid-argument on the token both mean this device
            // is gone. Reporting it as dead is what lets the channel deactivate the row rather than
            // retrying a token that will never work again.
            var isGone = response.StatusCode is HttpStatusCode.NotFound
                || (response.StatusCode is HttpStatusCode.BadRequest && MentionsToken(body));

            _logger.LogWarning(
                "Push to endpoint {EndpointId} failed with {Status}. Gone: {IsGone}.",
                target.EndpointId, (int)response.StatusCode, isGone);

            return new EndpointOutcome(target.EndpointId, Delivered: false, IsGone: isGone, Detail: $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Push to endpoint {EndpointId} threw.", target.EndpointId);
            return new EndpointOutcome(target.EndpointId, Delivered: false, IsGone: false, Detail: "TransportFailure");
        }
    }

    /// <summary>
    /// The FCM v1 message.
    /// <para>
    /// The route and the notification id go in <c>data</c> rather than in <c>notification</c>,
    /// because <c>data</c> is what the client receives in every state — foreground, background and
    /// terminated — and the deep link has to survive a tap from a cold start. Values are strings:
    /// FCM's data map is <c>map&lt;string,string&gt;</c> and a number here is rejected outright.
    /// </para>
    /// </summary>
    private string BuildPayload(ChannelEndpoint target, PushMessage message)
    {
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["notificationId"] = message.NotificationId.ToString("D"),
            ["category"] = message.CategoryKey,
        };

        if (!string.IsNullOrWhiteSpace(message.Route))
        {
            data["route"] = message.Route;
        }

        if (message.BadgeCount is { } badge)
        {
            data["badge"] = badge.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var androidPriority = message.IsTimeSensitive ? "HIGH" : "NORMAL";

        var payload = new
        {
            message = new
            {
                token = target.Address,
                notification = new { title = message.Title, body = message.Body },
                data,
                android = new
                {
                    priority = androidPriority,
                    // `Settings` is the provider's free key/value bag — the channel id belongs to
                    // the Android client, not to this module's schema, which is why it is read
                    // from there rather than given a typed option.
                    notification = new { channel_id = AndroidChannelId() },
                },
                apns = new
                {
                    headers = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        // 10 wakes the device, 5 lets the OS schedule it. Only Critical asks to wake.
                        ["apns-priority"] = message.IsTimeSensitive ? "10" : "5",
                    },
                },
            },
        };

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// One result for a message that may have reached some devices and not others.
    /// <para>
    /// Succeeded means <em>at least one</em> device took it, which is the honest reading: a member
    /// with a live phone and a stale tablet has been notified. Dead endpoints are reported
    /// regardless, so the stale one is deactivated either way.
    /// </para>
    /// </summary>
    private static PushSendResult Fold(IReadOnlyList<EndpointOutcome> outcomes, PushMessage message)
    {
        var dead = outcomes.Where(o => o.IsGone && o.EndpointId is not null)
            .Select(o => o.EndpointId!.Value)
            .ToArray();

        var delivered = outcomes.Where(o => o.Delivered).ToArray();

        if (delivered.Length > 0)
        {
            return PushSendResult.Success(delivered[0].Detail) with { DeadEndpoints = dead };
        }

        // Nothing landed. Permanent only if every endpoint is gone — otherwise the failure was a
        // transport or a provider problem and a retry can still succeed.
        var allGone = outcomes.All(o => o.IsGone);

        return PushSendResult.Failure(
            allGone ? "EndpointsGone" : "ProviderRejected",
            allGone
                ? "Every registered device for this member has been unregistered by the provider."
                : $"The provider accepted none of {outcomes.Count} endpoint(s).",
            isPermanent: allGone) with
        { DeadEndpoints = dead };
    }

    /// <summary>
    /// The Android notification channel banners are posted to. Must match a channel the client
    /// created — `app.json` declares `expo-notifications` with `defaultChannel: "default"` — or
    /// Android silently drops the banner on API 26 and above.
    /// </summary>
    private string AndroidChannelId()
    {
        var settings = _options.CurrentValue.Settings;

        return settings is not null
            && settings.TryGetValue("AndroidChannelId", out var configured)
            && !string.IsNullOrWhiteSpace(configured)
                ? configured
                : "default";
    }

    private static string? ReadMessageName(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("name", out var name) ? name.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool MentionsToken(string body) =>
        body.Contains("registration-token", StringComparison.OrdinalIgnoreCase)
        || body.Contains("INVALID_ARGUMENT", StringComparison.Ordinal);

    private sealed record EndpointOutcome(Guid? EndpointId, bool Delivered, bool IsGone, string? Detail);
}
