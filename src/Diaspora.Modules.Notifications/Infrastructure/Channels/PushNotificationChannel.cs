using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Infrastructure.Channels;

/// <summary>
/// Turns a notification into a push payload and hands it to whatever transport is registered.
/// <para>
/// <b>Everything here is policy, and none of it is a vendor.</b> Which endpoints to use, how long
/// the body may be, whether the message may wake a sleeping phone, what a rejected token means — all
/// decided here, once, and unchanged when Firebase is replaced by APNs or by both.
/// </para>
/// <para>
/// <b>Dead endpoints are reported even on success.</b> A multicast to four devices routinely
/// succeeds for three and reports the fourth unregistered; a platform that only looked at the
/// overall result would pay to fail on that fourth token forever.
/// </para>
/// </summary>
public sealed class PushNotificationChannel : INotificationChannel
{
    private readonly IPushNotificationSender _sender;
    private readonly ILogger<PushNotificationChannel> _logger;

    public PushNotificationChannel(
        IPushNotificationSender sender, ILogger<PushNotificationChannel> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public NotificationChannelKind Kind => NotificationChannelKind.Push;

    public bool IsAvailable => _sender.IsConfigured;

    public async Task<NotificationChannelOutcome> SendAsync(
        NotificationChannelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_sender.IsConfigured)
        {
            return NotificationChannelOutcome.NotConfigured();
        }

        if (request.Endpoints.Count == 0)
        {
            return NotificationChannelOutcome.NoEndpoint();
        }

        var message = new PushMessage
        {
            Endpoints = request.Endpoints,
            Title = request.Title,
            Body = Clip(request.Body),
            Route = request.Route,

            // Only Critical wakes a device. Every provider throttles this and every one of them
            // spells it differently, which is why it is one deliberate flag rather than the priority
            // passed straight through.
            IsTimeSensitive = request.Priority == NotificationPriority.Critical,
            NotificationId = request.NotificationId.Value,
            CategoryKey = request.CategoryKey
        };

        var result = await _sender.SendAsync(message, cancellationToken);

        if (result.Succeeded)
        {
            return NotificationChannelOutcome.Success(result.ProviderMessageId, result.DeadEndpoints);
        }

        _logger.LogWarning(
            "Push delivery for notification {NotificationId} failed: {ReasonCode}",
            request.NotificationId.Value,
            result.ReasonCode);

        return result.IsPermanent
            ? NotificationChannelOutcome.Permanent(
                result.ReasonCode ?? "Rejected", result.Detail, result.DeadEndpoints)
            : NotificationChannelOutcome.Transient(result.ReasonCode ?? "Unknown", result.Detail);
    }

    /// <summary>
    /// Trims to what a push payload can carry.
    /// <para>
    /// Trimmed here rather than at render time because the limit is a property of the transport, not
    /// of the words — an email of the same notification wants the whole body.
    /// </para>
    /// </summary>
    private static string Clip(string body) =>
        body.Length <= NotificationLimits.MaxPushBodyLength
            ? body
            : string.Concat(body.AsSpan(0, NotificationLimits.MaxPushBodyLength - 1).TrimEnd(), "…");
}
