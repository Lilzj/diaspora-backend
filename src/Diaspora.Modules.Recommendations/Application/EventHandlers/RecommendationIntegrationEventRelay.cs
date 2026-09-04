using Diaspora.Contracts.Events;
using Diaspora.Contracts.Recommendations.V1.Events;
using Diaspora.Modules.Recommendations.Domain.Events;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Recommendations.Application.EventHandlers;

/// <summary>
/// Turns B14's domain events into the published contracts other modules consume.
/// <para>
/// <b>The one translation seam, and the reason this module's domain stays private.</b> A domain
/// event carries strongly typed identifiers and domain enums; a published contract carries flat
/// primitives a consumer can parse without referencing this module. Doing it in a single class means
/// the two cannot drift and a refactor inside an aggregate cannot change somebody else's contract.
/// </para>
/// <para>
/// <b>Nothing published from here says what a member was recommended.</b> A profile is the most
/// inferential thing the platform holds about somebody, and a page of recommendations is a
/// derivative of it; putting either on a bus every module reads would make this module the
/// platform's largest unnecessary disclosure surface. What travels is shape: counts, kinds,
/// completeness, strategy keys. The one exception is an acceptance, which names a target — and only
/// because the member has already told the owning module about it directly by acting.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The aggregate is already saved by the time this
/// runs; throwing would roll back a member dismissing a card because an analytics consumer was
/// unreachable. At-least-once is the contract, and <c>EventId</c> is the idempotency key — which is
/// why no payload here declares a property called <c>EventId</c>. The base <c>IntegrationEvent</c>
/// owns that name, and shadowing it compiles with a warning while silently breaking every consumer's
/// de-duplication.
/// </para>
/// </summary>
public sealed class RecommendationIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<RecommendationIntegrationEventRelay> _logger;

    public RecommendationIntegrationEventRelay(
        IIntegrationEventPublisher publisher, ILogger<RecommendationIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task PublishAsync(
        RecommendationGeneratedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new RecommendationGeneratedIntegrationEvent
            {
                SetId = domainEvent.SetId.Value,
                UserId = domainEvent.UserId.Value,
                Surface = domainEvent.Surface.ToString(),
                ItemCount = domainEvent.ItemCount,
                StrategyKeys = domainEvent.StrategyKeys,
                IsColdStart = domainEvent.IsColdStart,
                PolicyExclusionCount = domainEvent.PolicyExclusionCount,

                // Copied rather than recomputed. This relay is the boundary, and a second place
                // that decided what counts as promoted would be a second answer to the question
                // B15 bills on.
                PromotedTargets = [.. domainEvent.PromotedTargets.Select(target =>
                    new PromotedTargetPayload
                    {
                        TargetType = target.TargetType.ToString(),
                        TargetId = target.TargetId,
                        Position = target.Position
                    })],

                GeneratedAt = domainEvent.GeneratedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        RecommendationAcceptedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new RecommendationAcceptedIntegrationEvent
            {
                FeedbackId = domainEvent.FeedbackId.Value,
                UserId = domainEvent.UserId.Value,
                TargetType = domainEvent.TargetType.ToString(),
                TargetId = domainEvent.TargetId,
                Surface = domainEvent.Surface.ToString(),
                StrategyKey = domainEvent.StrategyKey,
                ReasonCode = domainEvent.ReasonCode?.ToString(),
                Score = domainEvent.Score,
                AcceptedAt = domainEvent.AcceptedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        RecommendationDismissedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new RecommendationDismissedIntegrationEvent
            {
                FeedbackId = domainEvent.FeedbackId.Value,
                UserId = domainEvent.UserId.Value,
                TargetType = domainEvent.TargetType.ToString(),
                TargetId = domainEvent.TargetId,
                Surface = domainEvent.Surface.ToString(),
                StrategyKey = domainEvent.StrategyKey,
                ReasonCode = domainEvent.ReasonCode?.ToString(),
                SuppressedUntil = domainEvent.SuppressedUntil,
                DismissedAt = domainEvent.DismissedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        PersonalizationProfileUpdatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new PersonalizationProfileUpdatedIntegrationEvent
            {
                ProfileId = domainEvent.ProfileId.Value,
                UserId = domainEvent.UserId.Value,
                Completeness = domainEvent.Completeness,
                IsColdStart = domainEvent.IsColdStart,
                SignalCount = domainEvent.SignalCount,
                WasRebuilt = domainEvent.WasRebuilt,
                UpdatedAt = domainEvent.UpdatedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        SignalsRecordedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new SignalsRecordedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                SignalCount = domainEvent.SignalCount,
                Kinds = domainEvent.Kinds.Select(kind => kind.ToString()).ToList(),
                Source = domainEvent.Source.ToString(),
                RecordedAt = domainEvent.RecordedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        RecommendationPolicyChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new RecommendationPolicyChangedIntegrationEvent
            {
                PolicyId = domainEvent.PolicyId.Value,
                PolicyKey = domainEvent.PolicyKey,
                Kind = domainEvent.Kind.ToString(),
                Effect = domainEvent.Effect.ToString(),
                IsEnabled = domainEvent.IsEnabled,
                ChangeType = domainEvent.ChangeType,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// Enqueues, and never lets a failure reach the caller.
    /// <para>
    /// See the class summary. The aggregate is already saved; failing here would roll back a
    /// member's action because a consumer was unreachable.
    /// </para>
    /// </summary>
    private async Task PublishSafelyAsync(
        IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
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
                "Failed to enqueue {EventType}. The aggregate was saved and the announcement was "
                + "lost.",
                integrationEvent.GetType().Name);
        }
    }
}
