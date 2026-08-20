using Diaspora.Contracts.Notifications.V1.Events;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// Turns B13's domain events into the published contracts other modules consume.
/// <para>
/// <b>The one translation seam, and the reason this module's domain stays private.</b> A domain
/// event carries strongly typed identifiers and domain enums; a published contract carries flat
/// primitives a consumer can parse without referencing this module. Doing it in a single class means
/// the two cannot drift and a refactor inside an aggregate cannot change somebody else's contract.
/// </para>
/// <para>
/// <b>Nothing published from here carries a notification's words.</b> A message's text is the most
/// personal thing this module holds — it names the person who messaged you, the flat you applied
/// for, the offer you were made — and putting it on a bus every module reads would make the
/// communication platform the platform's largest unnecessary disclosure surface. Consumers get
/// identifiers, categories and timings.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The aggregate is already saved by the time this
/// runs; throwing would roll back a member marking something read because an analytics consumer was
/// unreachable. At-least-once is the contract, and <c>EventId</c> is the idempotency key — which is
/// why no payload here declares a property called <c>EventId</c>. The base
/// <c>IntegrationEvent</c> owns that name, and shadowing it compiles with a warning while silently
/// breaking every consumer's de-duplication.
/// </para>
/// </summary>
public sealed class NotificationIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<NotificationIntegrationEventRelay> _logger;

    public NotificationIntegrationEventRelay(
        IIntegrationEventPublisher publisher, ILogger<NotificationIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task PublishAsync(
        NotificationCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new NotificationCreatedIntegrationEvent
            {
                NotificationId = domainEvent.NotificationId.Value,
                RecipientUserId = domainEvent.RecipientUserId.Value,
                CategoryKey = domainEvent.CategoryKey,
                Priority = domainEvent.Priority.ToString(),
                TemplateKey = domainEvent.TemplateKey,
                RuleKey = domainEvent.RuleKey,
                SourceModule = domainEvent.SourceModule,
                SourceEventName = domainEvent.SourceEventName,
                SubjectId = domainEvent.SubjectId,
                CreatedAt = domainEvent.CreatedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        NotificationQueuedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new NotificationQueuedIntegrationEvent
            {
                NotificationId = domainEvent.NotificationId.Value,
                RecipientUserId = domainEvent.RecipientUserId.Value,
                Channel = domainEvent.Channel.ToString(),
                CategoryKey = domainEvent.CategoryKey,
                Priority = domainEvent.Priority.ToString(),
                DeliverAfter = domainEvent.DeliverAfter,
                QueuedAt = domainEvent.QueuedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        NotificationDeliveredDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new NotificationDeliveredIntegrationEvent
            {
                NotificationId = domainEvent.NotificationId.Value,
                RecipientUserId = domainEvent.RecipientUserId.Value,
                Channel = domainEvent.Channel.ToString(),
                CategoryKey = domainEvent.CategoryKey,
                Attempt = domainEvent.Attempt,
                ProviderMessageId = domainEvent.ProviderMessageId,
                DeliveredAt = domainEvent.DeliveredAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        NotificationFailedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new NotificationFailedIntegrationEvent
            {
                NotificationId = domainEvent.NotificationId.Value,
                RecipientUserId = domainEvent.RecipientUserId.Value,
                Channel = domainEvent.Channel.ToString(),
                CategoryKey = domainEvent.CategoryKey,
                Attempts = domainEvent.Attempts,
                ReasonCode = domainEvent.ReasonCode,
                IsPermanent = domainEvent.IsPermanent,
                FailedAt = domainEvent.FailedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        NotificationOpenedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new NotificationOpenedIntegrationEvent
            {
                NotificationId = domainEvent.NotificationId.Value,
                RecipientUserId = domainEvent.RecipientUserId.Value,
                CategoryKey = domainEvent.CategoryKey,
                TemplateKey = domainEvent.TemplateKey,
                Channel = domainEvent.Channel,
                MinutesToOpen = domainEvent.MinutesToOpen,
                OpenedAt = domainEvent.OpenedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        NotificationClickedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new NotificationClickedIntegrationEvent
            {
                NotificationId = domainEvent.NotificationId.Value,
                RecipientUserId = domainEvent.RecipientUserId.Value,
                CategoryKey = domainEvent.CategoryKey,
                TemplateKey = domainEvent.TemplateKey,
                TargetRoute = domainEvent.TargetRoute,
                ActionKey = domainEvent.ActionKey,
                ClickedAt = domainEvent.ClickedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        NotificationArchivedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new NotificationArchivedIntegrationEvent
            {
                NotificationId = domainEvent.NotificationId.Value,
                RecipientUserId = domainEvent.RecipientUserId.Value,
                CategoryKey = domainEvent.CategoryKey,
                Reason = domainEvent.Reason.ToString(),
                ByRecipient = domainEvent.ByRecipient,
                ArchivedAt = domainEvent.ArchivedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        DigestGeneratedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new DigestGeneratedIntegrationEvent
            {
                DigestId = domainEvent.DigestId.Value,
                RecipientUserId = domainEvent.RecipientUserId.Value,
                Frequency = domainEvent.Frequency.ToString(),
                CategoryKey = domainEvent.CategoryKey,
                CountryCode = domainEvent.CountryCode,
                ItemCount = domainEvent.ItemCount,
                WindowStart = domainEvent.WindowStart,
                WindowEnd = domainEvent.WindowEnd,
                GeneratedAt = domainEvent.GeneratedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A member changed how they want to be contacted.
    /// <para>
    /// Takes the collected sections rather than one domain event, because a member changing four
    /// switches on one screen is one fact to everybody outside this module — and four published
    /// events would make a consumer counting opt-outs count the same member four times.
    /// </para>
    /// </summary>
    public Task PublishAsync(
        Guid userId,
        IReadOnlyList<string> changedSections,
        bool isFullyOptedOut,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changedSections);

        return PublishSafelyAsync(
            new PreferenceChangedIntegrationEvent
            {
                UserId = userId,
                ChangedSections = changedSections,
                IsFullyOptedOut = isFullyOptedOut,
                ChangedAt = changedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        DeviceRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new DeviceRegisteredIntegrationEvent
            {
                DeviceRegistrationId = domainEvent.DeviceRegistrationId.Value,
                UserId = domainEvent.UserId.Value,
                Platform = domainEvent.Platform.ToString(),
                Channel = domainEvent.Channel.ToString(),
                DeviceIdentifier = domainEvent.DeviceIdentifier,
                IsNewRegistration = domainEvent.IsNewRegistration,
                RegisteredAt = domainEvent.RegisteredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// Publishes, and swallows whatever goes wrong.
    /// <para>
    /// The aggregate is already saved by the time a relay runs. Letting an exception out would roll
    /// back a member's own action because a publish failed, which trades a certain loss for a
    /// possible one.
    /// </para>
    /// </summary>
    private async Task PublishSafelyAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : Diaspora.Contracts.Events.IIntegrationEvent
    {
        try
        {
            await _publisher.PublishAsync(integrationEvent, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogError(
                exception,
                "Failed to publish {ContractName} from the Notifications module. The write is unaffected.",
                typeof(TEvent).Name);
        }
    }
}
