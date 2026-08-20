using Diaspora.Contracts.Conversations.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Modules.Conversations.Domain.Events;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Conversations.Application.EventHandlers;

/// <summary>
/// The single point where a domain fact becomes a published contract.
/// <para>
/// <b>One class rather than a publish call in each handler.</b> Every module that let its handlers
/// publish directly ended up with two of them naming the same event slightly differently, and the
/// consumer that broke was in another assembly. Concentrating it here also means the outbox is
/// written from one place, so "never bypass the outbox" is checkable by reading one file.
/// </para>
/// <para>
/// <b>Publishing never fails a write.</b> The domain fact is committed by the time these run — the
/// dispatcher runs after <c>SaveChanges</c> — so a publish failure is a message that is late, not a
/// conversation that is lost. The outbox itself retries; this only guards against the enqueue
/// throwing.
/// </para>
/// </summary>
public sealed class ConversationIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<ConversationIntegrationEventRelay> _logger;

    public ConversationIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        ILogger<ConversationIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task PublishAsync(
        ConversationCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationCreatedIntegrationEvent
            {
                ConversationId = domainEvent.ConversationId.Value,
                ContextType = domainEvent.ContextTypeKey,
                EntityId = domainEvent.EntityId,
                InitiatorUserId = domainEvent.InitiatorUserId.Value,
                ParticipantUserIds =
                    [.. domainEvent.ParticipantUserIds.Select(userId => userId.Value)],
                Status = domainEvent.Status.ToString(),
                CreatedAt = domainEvent.CreatedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        MessageEditedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationMessageEditedIntegrationEvent
            {
                ConversationId = domainEvent.ConversationId.Value,
                MessageId = domainEvent.MessageId.Value,
                SenderUserId = domainEvent.SenderId.Value,
                EditedAt = domainEvent.EditedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        MessageDeletedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationMessageDeletedIntegrationEvent
            {
                ConversationId = domainEvent.ConversationId.Value,
                MessageId = domainEvent.MessageId.Value,
                SenderUserId = domainEvent.SenderId.Value,
                DeletedAt = domainEvent.DeletedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        MessagesReadDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationMessageReadIntegrationEvent
            {
                ConversationId = domainEvent.ConversationId.Value,
                ReaderUserId = domainEvent.ReaderUserId.Value,
                ThroughMessageId = domainEvent.ThroughMessageId?.Value,
                ClearedCount = domainEvent.ClearedCount,
                ReadAt = domainEvent.ReadAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ConversationArchivedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationArchivedIntegrationEvent
            {
                ConversationId = domainEvent.ConversationId.Value,
                UserId = domainEvent.UserId.Value,
                ArchivedAt = domainEvent.ArchivedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ConversationRestoredDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationRestoredIntegrationEvent
            {
                ConversationId = domainEvent.ConversationId.Value,
                UserId = domainEvent.UserId.Value,
                RestoredAt = domainEvent.RestoredAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ConversationMutedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationMutedIntegrationEvent
            {
                ConversationId = domainEvent.ConversationId.Value,
                UserId = domainEvent.UserId.Value,
                IsMuted = domainEvent.IsMuted,
                MutedUntil = domainEvent.MutedUntil,
                OccurredOn = domainEvent.OccurredOn
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ConversationPinnedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationPinnedIntegrationEvent
            {
                ConversationId = domainEvent.ConversationId.Value,
                UserId = domainEvent.UserId.Value,
                IsPinned = domainEvent.IsPinned,
                OccurredOn = domainEvent.OccurredOn
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ParticipantRemovedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationParticipantRemovedIntegrationEvent
            {
                ConversationId = domainEvent.ConversationId.Value,
                UserId = domainEvent.UserId.Value,
                RemovedAt = domainEvent.RemovedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ConversationRequestAcceptedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationRequestAcceptedIntegrationEvent
            {
                ConversationId = domainEvent.ConversationId.Value,
                AcceptedByUserId = domainEvent.AcceptedByUserId.Value,
                InitiatorUserId = domainEvent.InitiatorUserId.Value,
                AcceptedAt = domainEvent.AcceptedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ConversationBlockedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationBlockedIntegrationEvent
            {
                BlockerUserId = domainEvent.BlockerUserId.Value,
                BlockedUserId = domainEvent.BlockedUserId.Value,
                BlockedAt = domainEvent.BlockedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ConversationUnblockedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ConversationUnblockedIntegrationEvent
            {
                BlockerUserId = domainEvent.BlockerUserId.Value,
                BlockedUserId = domainEvent.BlockedUserId.Value,
                UnblockedAt = domainEvent.UnblockedAt
            },
            cancellationToken);
    }

    private async Task PublishSafelyAsync(
        IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishAsync(integrationEvent, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // The fact is committed. See the class summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Publishing {Event} failed. The conversation change is committed.",
                integrationEvent.GetType().Name);
        }
#pragma warning restore CA1031
    }
}
