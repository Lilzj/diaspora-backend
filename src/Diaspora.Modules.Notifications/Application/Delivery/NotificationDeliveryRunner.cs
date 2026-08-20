using System.Diagnostics;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Extensibility.Devices;
using Diaspora.Modules.Notifications.Infrastructure.Channels;
using Diaspora.Modules.Notifications.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Application.Delivery;

/// <summary>
/// Hands due deliveries to their channels and records what came back.
/// <para>
/// <b>Separate from the dispatcher because creating a message and moving it are different problems
/// with different failure modes.</b> Creation happens inside the transaction of the event that
/// caused it and must be fast and certain. Delivery talks to networks, takes seconds, fails
/// routinely and retries — and doing it inline would hold a database transaction open across a call
/// to somebody else's service.
/// </para>
/// <para>
/// <b>One notification per unit of work.</b> A batch that saved every result together would lose the
/// whole batch when one aggregate hit a concurrency conflict — and concurrency conflicts here are
/// ordinary, because the member is reading the same rows on their phone while the sweep writes them.
/// </para>
/// </summary>
public sealed class NotificationDeliveryRunner
{
    private readonly INotificationRepository _notifications;
    private readonly NotificationChannelRegistry _channels;
    private readonly IDeviceRegistry _devices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly ILogger<NotificationDeliveryRunner> _logger;

    public NotificationDeliveryRunner(
        INotificationRepository notifications,
        NotificationChannelRegistry channels,
        IDeviceRegistry devices,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock,
        ILogger<NotificationDeliveryRunner> logger)
    {
        _notifications = notifications;
        _channels = channels;
        _devices = devices;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Runs one pass. Returns how many deliveries were attempted, for the sweep's own logging.
    /// </summary>
    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var due = await _notifications.ListDueForDeliveryAsync(now, batchSize, cancellationToken);
        var attempted = 0;

        foreach (var notification in due)
        {
            attempted += await DeliverAsync(notification, cancellationToken);
        }

        return attempted;
    }

    /// <summary>
    /// Every due channel on one message.
    /// <para>
    /// The in-app channel is included and immediately succeeds, which is deliberate: it makes the
    /// delivery record uniform, so a support question about any message is answered by reading one
    /// table rather than by knowing which channels bother to write to it.
    /// </para>
    /// </summary>
    public async Task<int> DeliverAsync(
        Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var now = _clock.UtcNow;
        var attempted = 0;

        foreach (var delivery in notification.Deliveries.ToList())
        {
            if (delivery.IsTerminal || delivery.NextAttemptAt is not { } due || due > now)
            {
                continue;
            }

            await AttemptAsync(notification, delivery, cancellationToken);
            attempted++;
        }

        if (attempted > 0)
        {
            _notifications.Update(notification);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return attempted;
    }

    private async Task AttemptAsync(
        Notification notification,
        NotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        var channel = _channels.Find(delivery.Channel);
        var now = _clock.UtcNow;

        if (channel is null || !channel.IsAvailable)
        {
            // The channel vanished between planning and delivery — a provider was deconfigured, or a
            // rule proposed a kind nothing implements. Recorded once rather than retried.
            notification.MarkChannelUnsupported(_guids.NewSequentialGuid(), delivery.Channel, now);
            return;
        }

        if (!notification.BeginDelivery(delivery.Channel, now))
        {
            return;
        }

        var endpoints = await _devices.ResolveAsync(
            notification.RecipientUserId, delivery.Channel, cancellationToken);

        var request = BuildRequest(notification, delivery, endpoints);
        var stopwatch = Stopwatch.StartNew();

        NotificationChannelOutcome outcome;

        try
        {
            outcome = await channel.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A shutdown mid-flight. The delivery stays where it is and the next pass picks it up;
            // recording a failure here would consume an attempt for something that never happened.
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // A channel is a seam and may be implemented by anybody. One that throws must cost its
            // own delivery and never the batch — which is the entire reason this catch is broad.
            _logger.LogError(
                exception,
                "Channel {Channel} threw while delivering notification {NotificationId}.",
                delivery.Channel,
                notification.Id.Value);

            outcome = NotificationChannelOutcome.Transient("ChannelThrew", exception.GetType().Name);
        }
        finally
        {
            NotificationTelemetry.DeliveryDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("channel", delivery.Channel.ToString()));
        }

        await RecordAsync(notification, delivery, outcome, endpoints, cancellationToken);
    }

    private async Task RecordAsync(
        Notification notification,
        NotificationDelivery delivery,
        NotificationChannelOutcome outcome,
        IReadOnlyList<ChannelEndpoint> endpoints,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // Dead endpoints are retired whether the send succeeded or not: a multicast to four devices
        // routinely succeeds for three and reports the fourth unregistered.
        if (outcome.DeadEndpoints.Count > 0)
        {
            await _devices.RetireAsync(
                outcome.DeadEndpoints, outcome.ReasonCode ?? "Rejected", cancellationToken);

            NotificationTelemetry.DevicesRetired.Add(outcome.DeadEndpoints.Count);
        }

        if (outcome.Succeeded)
        {
            notification.RecordDelivered(
                delivery.Channel, _guids.NewSequentialGuid(), outcome.ProviderMessageId, now);

            var live = endpoints
                .Where(endpoint => endpoint.EndpointId is { } id && !outcome.DeadEndpoints.Contains(id))
                .Select(endpoint => endpoint.EndpointId!.Value)
                .ToList();

            if (live.Count > 0)
            {
                await _devices.RecordSuccessAsync(live, cancellationToken);
            }

            NotificationTelemetry.Delivered.Add(
                1, new KeyValuePair<string, object?>("channel", delivery.Channel.ToString()));

            return;
        }

        notification.RecordFailure(
            delivery.Channel,
            _guids.NewSequentialGuid(),
            outcome.ReasonCode ?? "Unknown",
            outcome.Detail,
            outcome.IsPermanent,
            now);

        NotificationTelemetry.Failed.Add(
            1,
            new KeyValuePair<string, object?>("channel", delivery.Channel.ToString()),
            new KeyValuePair<string, object?>("reason", outcome.ReasonCode ?? "Unknown"));
    }

    /// <summary>
    /// Flattens the aggregate into what a channel is allowed to see.
    /// <para>
    /// The push body is chosen here rather than by the channel, because whether a short form exists
    /// is a property of the rendered content and every channel guessing separately is how a member
    /// gets a truncated word on one device and a complete sentence on another.
    /// </para>
    /// </summary>
    private static NotificationChannelRequest BuildRequest(
        Notification notification,
        NotificationDelivery delivery,
        IReadOnlyList<ChannelEndpoint> endpoints) =>
        new()
        {
            NotificationId = notification.Id,
            RecipientUserId = notification.RecipientUserId,
            CategoryKey = notification.CategoryKey,
            Priority = notification.Priority,
            TemplateKey = notification.TemplateKey,
            Title = notification.Content.Title,
            Body = delivery.Channel == NotificationChannelKind.Push
                ? notification.Content.PushBody
                : notification.Content.Body,
            Language = notification.Content.Language,
            Format = notification.Content.Format,
            Route = notification.PrimaryLink?.Route,
            ImageMediaId = notification.Content.ImageMediaId,
            Actions = notification.Actions
                .Select(action => new ChannelAction
                {
                    Key = action.Key,
                    Label = action.Label,
                    Route = action.Link?.Route,
                    Style = action.Style.ToString()
                })
                .ToList(),
            Endpoints = endpoints,
            Attempt = delivery.AttemptCount
        };
}
