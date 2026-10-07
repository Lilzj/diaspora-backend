using Diaspora.Contracts.Events;
using Diaspora.Contracts.Intelligence.V1.Events;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.Intelligence.EventHandlers;

/// <summary>
/// Turns Country Intelligence's domain events into the published contracts other modules consume.
/// <para>
/// <b>The one translation seam, and the reason B8.2's domain stays private.</b> A domain event carries
/// strongly typed identifiers and domain enums; an integration event carries flat primitives, so a
/// consumer can parse it without referencing this module. Doing the translation in a single class
/// means the two never drift, and means a consumer's contract cannot be changed by a refactor inside
/// an aggregate.
/// </para>
/// <para>
/// <b>Publication fans out to up to three contracts, and this is the only place that decides.</b>
/// Every published notice raises the general contract; a notice whose category declares the emergency
/// or government alert family raises a second one on top. Three subscriptions racing to make that
/// decision independently would eventually disagree about which — and the one that matters is the
/// emergency, on the day it matters.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The notice is already saved by the time this runs;
/// throwing here would roll back a successful publication because a notification did not go out.
/// Failures are logged, and at-least-once delivery is the contract — consumers treat <c>EventId</c>
/// as the idempotency key.
/// </para>
/// </summary>
public sealed class IntelligenceIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly INoticeCategoryRepository _categories;
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly ILogger<IntelligenceIntegrationEventRelay> _logger;

    public IntelligenceIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        INoticeCategoryRepository categories,
        IIssuingAuthorityRepository authorities,
        ILogger<IntelligenceIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _categories = categories;
        _authorities = authorities;
        _logger = logger;
    }

    /// <summary>
    /// Everything that happens to a notice.
    /// <para>
    /// <b>It reads the authority back, and B13.1 records why that is the right shape.</b> Three
    /// routes exist to resolving a fact a contract needs: the aggregate stamps it at raise time, the
    /// handler passes it, or the relay reads it. The authority's <em>name</em> cannot be stamped —
    /// it changes independently of the notice and stamping it would freeze a ministry's old name onto
    /// every notice it ever issued — so the relay reads it, on the publication path only.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        OfficialNoticeChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        switch (domainEvent.Change)
        {
            case IntelligenceChangeKinds.Published:
                await PublishPublicationAsync(domainEvent, cancellationToken);
                break;

            case IntelligenceChangeKinds.Superseded:
                await PublishSupersessionAsync(domainEvent, cancellationToken);
                break;

            case IntelligenceChangeKinds.Archived:
                await PublishArchivalAsync(domainEvent, cancellationToken);
                break;

            default:
                await PublishUpdateAsync(domainEvent, cancellationToken);
                break;
        }
    }

    public Task PublishAsync(
        NoticeCategoryChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new NoticeCategoryUpdatedIntegrationEvent
            {
                NoticeCategoryId = domainEvent.NoticeCategoryId.Value,
                Key = domainEvent.Key,
                Name = domainEvent.Name,
                Change = domainEvent.Change,
                IsActive = domainEvent.IsActive,
                DefaultSeverity = IntelligenceWireNames.ToWire(domainEvent.DefaultSeverity),
                ChangedAt = domainEvent.OccurredAt,
                ChangedBy = domainEvent.ChangedBy?.Value
            },
            cancellationToken);
    }

    public Task PublishAsync(
        IssuingAuthorityChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new IssuingAuthorityUpdatedIntegrationEvent
            {
                IssuingAuthorityId = domainEvent.IssuingAuthorityId.Value,
                CountryCode = domainEvent.CountryCode,
                Name = domainEvent.Name,
                AuthorityType = IntelligenceWireNames.ToWire(domainEvent.AuthorityType),
                Change = domainEvent.Change,
                IsVerified = domainEvent.IsVerified,
                ChangedAt = domainEvent.OccurredAt,
                ChangedBy = domainEvent.ChangedBy?.Value
            },
            cancellationToken);
    }

    public Task PublishAsync(
        EmbassyChangedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new EmbassyUpdatedIntegrationEvent
            {
                EmbassyId = domainEvent.EmbassyId.Value,
                CountryCode = domainEvent.CountryCode,
                ServesCountryCode = domainEvent.ServesCountryCode,
                Name = domainEvent.Name,
                Change = domainEvent.Change,
                IsVerified = domainEvent.IsVerified,
                ChangedAt = domainEvent.OccurredAt,
                ChangedBy = domainEvent.ChangedBy?.Value
            },
            cancellationToken);
    }

    public Task PublishAsync(
        EmergencyDirectoryChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new EmergencyInformationUpdatedIntegrationEvent
            {
                CountryCode = domainEvent.CountryCode,
                Change = domainEvent.Change,
                ContactCount = domainEvent.ContactCount,
                ChangedAt = domainEvent.OccurredAt,
                ChangedBy = domainEvent.ChangedBy?.Value
            },
            cancellationToken);
    }

    public Task PublishAsync(
        VisaAdvisoryChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new VisaPolicyUpdatedIntegrationEvent
            {
                VisaAdvisoryId = domainEvent.VisaAdvisoryId.Value,
                CountryCode = domainEvent.CountryCode,
                AppliesToCountryCode = domainEvent.AppliesToCountryCode,
                Title = domainEvent.Title,
                VisaCategory = domainEvent.VisaCategory,
                Change = domainEvent.Change,
                Status = IntelligenceWireNames.ToWire(domainEvent.Status),
                AuthorityId = domainEvent.AuthorityId.Value,
                EffectiveFrom = domainEvent.EffectiveFrom,
                ExpiresAt = domainEvent.ExpiresAt,
                ChangedAt = domainEvent.OccurredAt,
                ChangedBy = domainEvent.ChangedBy?.Value
            },
            cancellationToken);
    }

    public Task PublishAsync(
        PublicHolidayChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new PublicHolidayPublishedIntegrationEvent
            {
                PublicHolidayId = domainEvent.PublicHolidayId.Value,
                CountryCode = domainEvent.CountryCode,
                Name = domainEvent.Name,
                Kind = IntelligenceWireNames.ToWire(domainEvent.Kind),
                ObservedOn = domainEvent.ObservedOn,
                ObservedUntil = domainEvent.ObservedUntil,
                Change = domainEvent.Change,
                ClosesGovernmentOffices = domainEvent.ClosesGovernmentOffices,
                ClosesEmbassies = domainEvent.ClosesEmbassies,
                ChangedAt = domainEvent.OccurredAt,
                ChangedBy = domainEvent.ChangedBy?.Value
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Publication, which is the branch that earns the whole class
    // ════════════════════════════════════════════════════════════════════════════════════════

    private async Task PublishPublicationAsync(
        OfficialNoticeChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var authority = await _authorities.GetIncludingArchivedAsync(
            domainEvent.AuthorityId, cancellationToken);

        var category = await _categories.GetIncludingArchivedAsync(
            domainEvent.CategoryKey, cancellationToken);

        IntelligenceTelemetry.NoticesPublished.Add(
            1, new KeyValuePair<string, object?>("country", domainEvent.CountryCode));

        if (domainEvent.Severity == NoticeSeverity.Critical)
        {
            IntelligenceTelemetry.CriticalNoticesPublished.Add(
                1, new KeyValuePair<string, object?>("country", domainEvent.CountryCode));
        }

        await PublishSafelyAsync(
            new OfficialNoticePublishedIntegrationEvent
            {
                OfficialNoticeId = domainEvent.OfficialNoticeId.Value,
                CountryCode = domainEvent.CountryCode,
                CategoryKey = domainEvent.CategoryKey,
                Title = domainEvent.Title,
                Summary = domainEvent.Summary,
                Severity = IntelligenceWireNames.ToWire(domainEvent.Severity),
                AuthorityId = domainEvent.AuthorityId.Value,
                AuthorityName = authority?.Name ?? string.Empty,
                IsOfficial = authority?.IsOfficial ?? false,
                PublicationDate = domainEvent.PublicationDate,
                EffectiveFrom = domainEvent.EffectiveFrom,
                ExpiresAt = domainEvent.ExpiresAt,
                PublishedAt = domainEvent.OccurredAt,
                PublishedBy = domainEvent.ChangedBy?.Value
            },
            cancellationToken);

        // The second contract, when the category asks for one. Never instead of the first.
        switch (category?.AlertFamily)
        {
            case NoticeAlertFamily.Emergency:
                await PublishSafelyAsync(
                    new EmergencyAlertPublishedIntegrationEvent
                    {
                        OfficialNoticeId = domainEvent.OfficialNoticeId.Value,
                        CountryCode = domainEvent.CountryCode,
                        CategoryKey = domainEvent.CategoryKey,
                        Title = domainEvent.Title,
                        Summary = domainEvent.Summary,
                        Severity = IntelligenceWireNames.ToWire(domainEvent.Severity),
                        AuthorityId = domainEvent.AuthorityId.Value,
                        AuthorityName = authority?.Name ?? string.Empty,
                        EffectiveFrom = domainEvent.EffectiveFrom,
                        ExpiresAt = domainEvent.ExpiresAt,
                        PublishedAt = domainEvent.OccurredAt,
                        PublishedBy = domainEvent.ChangedBy?.Value
                    },
                    cancellationToken);
                break;

            case NoticeAlertFamily.GovernmentAnnouncement:
                await PublishSafelyAsync(
                    new GovernmentAnnouncementPublishedIntegrationEvent
                    {
                        OfficialNoticeId = domainEvent.OfficialNoticeId.Value,
                        CountryCode = domainEvent.CountryCode,
                        CategoryKey = domainEvent.CategoryKey,
                        Title = domainEvent.Title,
                        Summary = domainEvent.Summary,
                        Severity = IntelligenceWireNames.ToWire(domainEvent.Severity),
                        AuthorityId = domainEvent.AuthorityId.Value,
                        AuthorityName = authority?.Name ?? string.Empty,
                        AuthorityType = authority is null
                            ? string.Empty
                            : IntelligenceWireNames.ToWire(authority.AuthorityType),
                        EffectiveFrom = domainEvent.EffectiveFrom,
                        PublishedAt = domainEvent.OccurredAt,
                        PublishedBy = domainEvent.ChangedBy?.Value
                    },
                    cancellationToken);
                break;

            default:
                break;
        }
    }

    private Task PublishSupersessionAsync(
        OfficialNoticeChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        IntelligenceTelemetry.NoticesSuperseded.Add(
            1, new KeyValuePair<string, object?>("country", domainEvent.CountryCode));

        // A supersede with no replacement is impossible through the aggregate, which refuses it.
        // Guarding anyway rather than writing Guid.Empty is the B13.1 lesson: a non-nullable
        // identifier a publisher does not know invents one, and it is silent forever.
        return domainEvent.SupersededBy is null
            ? Task.CompletedTask
            : PublishSafelyAsync(
                new OfficialNoticeSupersededIntegrationEvent
                {
                    OfficialNoticeId = domainEvent.OfficialNoticeId.Value,
                    SupersededByNoticeId = domainEvent.SupersededBy.Value,
                    CountryCode = domainEvent.CountryCode,
                    CategoryKey = domainEvent.CategoryKey,
                    Title = domainEvent.Title,
                    Severity = IntelligenceWireNames.ToWire(domainEvent.Severity),
                    AuthorityId = domainEvent.AuthorityId.Value,
                    SupersededAt = domainEvent.OccurredAt,
                    ChangedBy = domainEvent.ChangedBy?.Value
                },
                cancellationToken);
    }

    private Task PublishArchivalAsync(
        OfficialNoticeChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        PublishSafelyAsync(
            new OfficialNoticeArchivedIntegrationEvent
            {
                OfficialNoticeId = domainEvent.OfficialNoticeId.Value,
                CountryCode = domainEvent.CountryCode,
                CategoryKey = domainEvent.CategoryKey,
                Title = domainEvent.Title,
                ArchivedAt = domainEvent.OccurredAt,
                ChangedBy = domainEvent.ChangedBy?.Value
            },
            cancellationToken);

    private Task PublishUpdateAsync(
        OfficialNoticeChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        PublishSafelyAsync(
            new OfficialNoticeUpdatedIntegrationEvent
            {
                OfficialNoticeId = domainEvent.OfficialNoticeId.Value,
                CountryCode = domainEvent.CountryCode,
                CategoryKey = domainEvent.CategoryKey,
                Title = domainEvent.Title,
                Change = domainEvent.Change,
                Status = IntelligenceWireNames.ToWire(domainEvent.Status),
                Severity = IntelligenceWireNames.ToWire(domainEvent.Severity),
                AuthorityId = domainEvent.AuthorityId.Value,
                EffectiveFrom = domainEvent.EffectiveFrom,
                ExpiresAt = domainEvent.ExpiresAt,
                ChangedAt = domainEvent.OccurredAt,
                ChangedBy = domainEvent.ChangedBy?.Value
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
