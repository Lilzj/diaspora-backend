using Diaspora.Contracts.Events;
using Diaspora.Contracts.Knowledge.V1.Events;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;

/// <summary>
/// Turns the Knowledge Directory's domain events into the published contracts other modules consume.
/// <para>
/// <b>The one translation seam, and the reason B8.1's domain stays private.</b> A domain event
/// carries strongly typed identifiers and domain enums; an integration event carries flat
/// primitives, so a consumer can parse it without referencing this module. Doing the translation in
/// a single class means the two never drift, and means a consumer's contract cannot be changed by a
/// refactor inside an aggregate.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The curation is already saved by the time this
/// runs; throwing here would roll back a successful arrangement because a notification did not go
/// out. Failures are logged, and at-least-once delivery is the contract — consumers treat
/// <c>EventId</c> as the idempotency key.
/// </para>
/// <para>
/// <b>Featuring is split into two contracts where B8's is one.</b> B8 carries a boolean because a
/// consumer maintaining one flag would otherwise subscribe twice. Here the payloads genuinely
/// differ: featuring places a guide at a position within a country and a category, and unfeaturing
/// removes it from wherever it was — a single contract would carry a position that means nothing
/// half the time.
/// </para>
/// </summary>
public sealed class KnowledgeIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<KnowledgeIntegrationEventRelay> _logger;

    public KnowledgeIntegrationEventRelay(
        IIntegrationEventPublisher publisher, ILogger<KnowledgeIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task PublishAsync(
        KnowledgeCategoryChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        KnowledgeTelemetry.DirectoryCurations.Add(
            1, new KeyValuePair<string, object?>("kind", "category"));

        return PublishSafelyAsync(
            new KnowledgeCategoryUpdatedIntegrationEvent
            {
                KnowledgeCategoryId = domainEvent.KnowledgeCategoryId.Value,
                Key = domainEvent.Key,
                Name = domainEvent.Name,
                Change = domainEvent.Change,
                IsActive = domainEvent.IsActive,
                SourceCategoryKeys = domainEvent.SourceCategoryKeys,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CountryDirectoryChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        KnowledgeTelemetry.DirectoryCurations.Add(
            1, new KeyValuePair<string, object?>("kind", "directory"));

        return PublishSafelyAsync(
            new CountryKnowledgeDirectoryUpdatedIntegrationEvent
            {
                CountryCode = domainEvent.CountryCode,
                Change = domainEvent.Change,
                IsPublished = domainEvent.IsPublished,
                CategoryCount = domainEvent.CategoryCount,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        LearningPathChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        KnowledgeTelemetry.DirectoryCurations.Add(
            1, new KeyValuePair<string, object?>("kind", "learning-path"));

        return PublishSafelyAsync(
            new LearningPathUpdatedIntegrationEvent
            {
                LearningPathId = domainEvent.LearningPathId.Value,
                Key = domainEvent.Key,
                CountryCode = domainEvent.CountryCode,
                Title = domainEvent.Title,
                Change = domainEvent.Change,
                Status = KnowledgeWireNames.ToWire(domainEvent.Status),
                StepCount = domainEvent.StepCount,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>Featuring and unfeaturing, onto the two contracts their payloads justify.</summary>
    public Task PublishAsync(
        CountryGuideFeatureChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        KnowledgeTelemetry.DirectoryCurations.Add(
            1, new KeyValuePair<string, object?>("kind", "featured"));

        if (!domainEvent.Featured)
        {
            return PublishSafelyAsync(
                new KnowledgeGuideUnfeaturedIntegrationEvent
                {
                    GuideId = domainEvent.GuideId.Value,
                    CountryCode = domainEvent.CountryCode,
                    KnowledgeCategoryKey = domainEvent.KnowledgeCategoryKey,
                    ChangedBy = domainEvent.ChangedBy.Value,
                    ChangedAt = domainEvent.OccurredAt
                },
                cancellationToken);
        }

        return PublishSafelyAsync(
            new KnowledgeGuideFeaturedIntegrationEvent
            {
                GuideId = domainEvent.GuideId.Value,
                CountryCode = domainEvent.CountryCode,
                KnowledgeCategoryKey = domainEvent.KnowledgeCategoryKey,
                Position = domainEvent.Position,
                Note = domainEvent.Note,
                ChangedBy = domainEvent.ChangedBy.Value,
                ChangedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A country's numbers moved.
    /// <para>
    /// Published from the counter projection rather than from a domain event, because counters raise
    /// none — a country with a busy jobs board must not produce a message per posting on top of the
    /// one Jobs already published. The caller decides a counter changed materially; this only
    /// translates it.
    /// </para>
    /// </summary>
    public Task PublishDashboardChangedAsync(
        string countryCode,
        string counter,
        int count,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default) =>
        PublishSafelyAsync(
            new CountryDashboardUpdatedIntegrationEvent
            {
                CountryCode = countryCode,
                Counter = counter,
                Count = count,
                ChangedAt = changedAt
            },
            cancellationToken);

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
