using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Observability;
using Diaspora.Modules.Notifications.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Infrastructure.Channels;

/// <summary>
/// A push transport that reports success and sends nothing. For development and for tests.
/// <para>
/// <b>Distinct from <see cref="UnconfiguredPushNotificationSender"/> in exactly one way, and it is
/// the way that matters.</b> The unconfigured sender reports <c>IsConfigured = false</c>, so the
/// policy resolver records the channel <c>Unsupported</c> and delivery never happens — which is the
/// honest answer for a deployment with no provider, and means the <em>configured</em> half of the
/// push path is never exercised anywhere. This one reports configured, so a delivery row is created,
/// attempted, marked delivered, counted in analytics, and the endpoint has its failure count
/// cleared. Everything happens except the network call.
/// </para>
/// <para>
/// <b>Refused outside Development and Testing by the startup validator.</b> A production deployment
/// running this would mark every push delivered while no phone ever lit up — a platform that is
/// confidently wrong, which is strictly worse than one honestly reporting that nothing is
/// configured. The refusal is a startup error rather than a warning for that reason.
/// </para>
/// <para>
/// <b>It logs the endpoint count, never an endpoint.</b> A push token is a credential for somebody
/// else's service, and a development log is the least protected place this platform writes to.
/// </para>
/// </summary>
public sealed class LoopbackPushNotificationSender : IPushNotificationSender
{
    private readonly IOptionsMonitor<PushProviderOptions> _options;
    private readonly ILogger<LoopbackPushNotificationSender> _logger;

    public LoopbackPushNotificationSender(
        IOptionsMonitor<PushProviderOptions> options,
        ILogger<LoopbackPushNotificationSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured => _options.CurrentValue.IsLoopback;

    public Task<PushSendResult> SendAsync(
        PushMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Endpoints.Count == 0)
        {
            return Task.FromResult(PushSendResult.Failure(
                "NoEndpoint", "The recipient has no usable push endpoint.", isPermanent: true));
        }

        _logger.LogInformation(
            "Loopback push: notification {NotificationId} in category {Category} to {Count} endpoint(s).",
            message.NotificationId,
            message.CategoryKey,
            message.Endpoints.Count);

        NotificationTelemetry.RealtimeSent.Add(
            1, new KeyValuePair<string, object?>("event", "LoopbackPush"));

        // A synthetic provider message id, prefixed so nothing downstream can mistake it for a real
        // one — an operator searching a provider console for it should find nothing and know why.
        return Task.FromResult(PushSendResult.Success(
            $"loopback:{message.NotificationId:D}"));
    }
}
