using Diaspora.Contracts.Events;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Domain.Events;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>
/// Turns B7's domain events into the published integration events other modules consume.
/// <para>
/// <b>The translation layer that keeps B7's domain private.</b> A domain event carries strongly
/// typed identifiers and domain enums; an integration event carries flat primitives, so a
/// consumer can parse it without referencing this module. Doing the translation in one place
/// means the two never drift, and means a consumer's contract cannot be changed by a refactor
/// inside the aggregate.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The provider is already saved by the time this
/// runs; throwing here would roll back a successful registration because a notification did not
/// go out. Failures are logged, and at-least-once delivery is the contract — consumers treat
/// <c>EventId</c> as the idempotency key.
/// </para>
/// <para>
/// <b>B13.1: every payload names the provider's owner, and it is sourced two different ways.</b>
/// The three facts the aggregate raises about itself — an edit, a status move, a Trust decision —
/// carry the owner stamped at raise time, because the aggregate is holding its own membership
/// already and asking the database for what is in front of it would be absurd. The two facts
/// somebody else's aggregate raises — a review, a recommendation — do not know the provider at
/// all, so those are the only two that read it back.
/// </para>
/// </summary>
public sealed class ProviderIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IProviderRepository _providers;
    private readonly IServiceCategoryRepository _categories;
    private readonly IClock _clock;
    private readonly ILogger<ProviderIntegrationEventRelay> _logger;

    public ProviderIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        IProviderRepository providers,
        IServiceCategoryRepository categories,
        IClock clock,
        ILogger<ProviderIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _providers = providers;
        _categories = categories;
        _clock = clock;
        _logger = logger;
    }

    public Task PublishAsync(
        ProviderRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ProviderCreatedIntegrationEvent
            {
                ProviderId = domainEvent.ProviderId.Value,
                OwnerId = domainEvent.OwnerId.Value,
                ProviderType = domainEvent.ProviderType.ToString(),
                CategoryId = domainEvent.CategoryId.Value,
                Name = domainEvent.Name,
                Slug = domainEvent.Slug,
                CountryCode = domainEvent.CountryCode,
                AreaId = domainEvent.AreaId,
                CreatedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// <b>PSR-1: the one payload that reads its category back.</b> The aggregate holds a
    /// <c>ServiceCategoryId</c> and not the slug, so stamping the key at raise time was not
    /// available — the second of the two routes the enrichment guide allows. The read is a single
    /// row from a catalogue of a few hundred that is cached per request and changes a handful of
    /// times a year, which is the cheapest read in this module.
    /// </summary>
    public async Task PublishAsync(
        ProviderPublishedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await PublishSafelyAsync(
            new ProviderPublishedIntegrationEvent
            {
                ProviderId = domainEvent.ProviderId.Value,
                OwnerId = domainEvent.OwnerId.Value,
                ProviderType = domainEvent.ProviderType.ToString(),
                CategoryId = domainEvent.CategoryId.Value,
                CategoryKey = await ResolveCategoryKeyAsync(
                    domainEvent.CategoryId, cancellationToken),
                Name = domainEvent.Name,
                Visibility = domainEvent.Visibility.ToString(),
                CountryCode = domainEvent.CountryCode,
                AreaId = domainEvent.AreaId,
                Latitude = domainEvent.Latitude,
                Longitude = domainEvent.Longitude,
                PublishedAt = domainEvent.OccurredAt
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The category's slug, or null when it cannot be read.
    /// <para>
    /// <b>Null rather than a throw, and null rather than the identifier.</b> A publish must never
    /// fail a write that already committed, so a category that has been deleted underneath a
    /// published provider degrades the payload instead of losing it. And null rather than
    /// stringifying the GUID, because a consumer can tell null apart from a key it does not
    /// recognise — which is exactly the mistake this enrichment exists to correct.
    /// </para>
    /// </summary>
    private async Task<string?> ResolveCategoryKeyAsync(
        ServiceCategoryId categoryId, CancellationToken cancellationToken)
    {
        try
        {
            var category = await _categories.GetAsync(categoryId, cancellationToken);

            return category?.Slug.Value;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A category read must never fail a publish. See the summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Reading category {CategoryId} while publishing a provider failed. The payload " +
                "carries the identifier and no key.",
                categoryId.Value);

            return null;
        }
#pragma warning restore CA1031
    }

    public Task PublishAsync(
        ProviderUpdatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ProviderUpdatedIntegrationEvent
            {
                ProviderId = domainEvent.ProviderId.Value,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedFields = domainEvent.ChangedFields,
                ChangedAt = domainEvent.OccurredAt,
                OwnerUserId = domainEvent.OwnerUserId?.Value
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ProviderStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ProviderStatusChangedIntegrationEvent
            {
                ProviderId = domainEvent.ProviderId.Value,
                PreviousStatus = domainEvent.PreviousStatus.ToString(),
                CurrentStatus = domainEvent.CurrentStatus.ToString(),
                ChangedBy = domainEvent.ChangedBy?.Value,
                Reason = domainEvent.Reason,
                ChangedAt = domainEvent.OccurredAt,
                OwnerUserId = domainEvent.OwnerUserId?.Value
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ProviderVerificationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ProviderVerificationChangedIntegrationEvent
            {
                ProviderId = domainEvent.ProviderId.Value,
                Kind = domainEvent.Kind.ToString(),
                PreviousState = domainEvent.PreviousState.ToString(),
                CurrentState = domainEvent.CurrentState.ToString(),
                IsVerifiedNow = domainEvent.IsVerifiedNow,
                ExpiresAt = domainEvent.ExpiresAt,
                ReasonCode = domainEvent.ReasonCode,
                ChangedAt = domainEvent.OccurredAt,
                OwnerUserId = domainEvent.OwnerUserId?.Value
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ProviderServiceAddedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ProviderServiceAddedIntegrationEvent
            {
                ProviderId = domainEvent.ProviderId.Value,
                ServiceId = domainEvent.ServiceId.Value,
                Name = domainEvent.Name,
                CategoryId = domainEvent.CategoryId?.Value,
                AddedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ProviderAvailabilityChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ProviderAvailabilityChangedIntegrationEvent
            {
                ProviderId = domainEvent.ProviderId.Value,
                Kind = domainEvent.Kind.ToString(),
                AffectsToday = domainEvent.AffectsToday,
                EffectiveFrom = domainEvent.EffectiveFrom,
                ChangedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A review landed.
    /// <para>
    /// <b>One of the two contracts that reads the provider back, and the reason is that the
    /// review aggregate does not know who owns the business it is about.</b> B13.1 put the
    /// owner on the payload so Communication can send "you have a new review" without asking
    /// this module who to send it to; that answer has to come from somewhere, and inside the
    /// publishing module is the only place it can come from without becoming a cross-module
    /// query.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        ProviderReviewSubmittedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await PublishSafelyAsync(
            new ProviderReviewReceivedIntegrationEvent
            {
                ReviewId = domainEvent.ReviewId.Value,
                ProviderId = domainEvent.ProviderId.Value,
                ReviewerId = domainEvent.ReviewerId.Value,
                Rating = domainEvent.Rating,
                HasBody = domainEvent.HasBody,
                ReceivedAt = domainEvent.OccurredAt,
                OwnerUserId = await OwnerOfAsync(domainEvent.ProviderId, cancellationToken)
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        ProviderRecommendationReceivedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await PublishSafelyAsync(
            new ProviderRecommendationReceivedIntegrationEvent
            {
                RecommendationId = domainEvent.RecommendationId.Value,
                ProviderId = domainEvent.ProviderId.Value,
                RecommenderId = domainEvent.RecommenderId.Value,
                Kind = domainEvent.Kind.ToString(),
                ReceivedAt = domainEvent.OccurredAt,
                OwnerUserId = await OwnerOfAsync(domainEvent.ProviderId, cancellationToken)
            },
            cancellationToken);
    }

    /// <summary>
    /// The provider's notifiable owner, or null.
    /// <para>
    /// Null is a normal answer, not a failure: a provider deleted between the write and this
    /// relay, or one loaded without its membership, yields nothing and the consumer sends
    /// nothing. Throwing here would fail a publish for a review that has already been saved.
    /// </para>
    /// </summary>
    private async Task<Guid?> OwnerOfAsync(ProviderId providerId, CancellationToken cancellationToken)
    {
        var provider = await _providers.GetAsync(providerId, cancellationToken);

        return provider?.NotifiableOwnerId?.Value;
    }

    private async Task PublishSafelyAsync<TEvent>(
        TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        try
        {
            await _publisher.PublishAsync(integrationEvent, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A failed publish must not roll back a successful write.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Publishing {EventType} failed. The write succeeded; the notification did not.",
                typeof(TEvent).Name);
        }
#pragma warning restore CA1031
    }
}
