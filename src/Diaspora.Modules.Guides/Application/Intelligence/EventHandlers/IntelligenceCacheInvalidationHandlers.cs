using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Extensibility.Caching;

namespace Diaspora.Modules.Guides.Application.Intelligence.EventHandlers;

/// <summary>
/// Drops a country's cached intelligence page whenever anything official about it changes.
/// <para>
/// <b>Separate handlers from the relay's, and the platform's analyzer pack is why.</b> A domain event
/// may be settled by exactly one handler — two subscriptions to one fact eventually disagree about it
/// — but invalidation is not settlement: these observe rather than translate, and the analyzer's rule
/// is about the handler that publishes the contract. Keeping them apart also means a cache failure
/// cannot stop an integration event going out, which on a critical alert is the one ordering that
/// matters.
/// </para>
/// <para>
/// <b>Invalidation is by country scope, not by key.</b> A publication changes the latest band, the
/// urgent band, possibly the visa band, the category breakdown and the statistics — one key per
/// locale each. The publisher cannot enumerate them; it knows only that everything about that country
/// is now suspect, which is precisely what the scope expresses.
/// </para>
/// </summary>
public sealed class NoticeCacheInvalidationHandler
    : DomainEventHandler<OfficialNoticeChangedDomainEvent>
{
    private readonly IKnowledgeCache _cache;

    public NoticeCacheInvalidationHandler(IKnowledgeCache cache) => _cache = cache;

    protected override Task HandleAsync(
        OfficialNoticeChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _cache.InvalidateAsync(
            IntelligenceCacheKeys.CountryScope(domainEvent.CountryCode), cancellationToken);
    }
}

/// <summary>Drops a country's cached page when a mission's entry changes.</summary>
public sealed class EmbassyCacheInvalidationHandler
    : DomainEventHandler<EmbassyChangedDomainEvent>
{
    private readonly IKnowledgeCache _cache;

    public EmbassyCacheInvalidationHandler(IKnowledgeCache cache) => _cache = cache;

    /// <summary>
    /// Both countries, because a mission appears on both — where it is, and whose nationals it
    /// serves. Dropping only one would leave the other's page showing an entry that has moved.
    /// </summary>
    protected override async Task HandleAsync(
        EmbassyChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _cache.InvalidateAsync(
            IntelligenceCacheKeys.CountryScope(domainEvent.CountryCode), cancellationToken);

        if (!string.Equals(
            domainEvent.ServesCountryCode, domainEvent.CountryCode, StringComparison.Ordinal))
        {
            await _cache.InvalidateAsync(
                IntelligenceCacheKeys.CountryScope(domainEvent.ServesCountryCode),
                cancellationToken);
        }
    }
}

/// <summary>Drops a country's cached page when its emergency information changes.</summary>
public sealed class EmergencyCacheInvalidationHandler
    : DomainEventHandler<EmergencyDirectoryChangedDomainEvent>
{
    private readonly IKnowledgeCache _cache;

    public EmergencyCacheInvalidationHandler(IKnowledgeCache cache) => _cache = cache;

    protected override Task HandleAsync(
        EmergencyDirectoryChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _cache.InvalidateAsync(
            IntelligenceCacheKeys.CountryScope(domainEvent.CountryCode), cancellationToken);
    }
}

/// <summary>Drops a country's cached page when a visa advisory changes.</summary>
public sealed class VisaAdvisoryCacheInvalidationHandler
    : DomainEventHandler<VisaAdvisoryChangedDomainEvent>
{
    private readonly IKnowledgeCache _cache;

    public VisaAdvisoryCacheInvalidationHandler(IKnowledgeCache cache) => _cache = cache;

    protected override Task HandleAsync(
        VisaAdvisoryChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _cache.InvalidateAsync(
            IntelligenceCacheKeys.CountryScope(domainEvent.CountryCode), cancellationToken);
    }
}

/// <summary>Drops a country's cached page when its calendar changes.</summary>
public sealed class PublicHolidayCacheInvalidationHandler
    : DomainEventHandler<PublicHolidayChangedDomainEvent>
{
    private readonly IKnowledgeCache _cache;

    public PublicHolidayCacheInvalidationHandler(IKnowledgeCache cache) => _cache = cache;

    protected override Task HandleAsync(
        PublicHolidayChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _cache.InvalidateAsync(
            IntelligenceCacheKeys.CountryScope(domainEvent.CountryCode), cancellationToken);
    }
}

/// <summary>
/// Drops a country's cached page when an authority's verification changes.
/// <para>
/// An authority becoming verified changes the official badge on every notice it has ever issued —
/// which is on every band of that country's page, and which nothing else would invalidate.
/// </para>
/// </summary>
public sealed class AuthorityCacheInvalidationHandler
    : DomainEventHandler<IssuingAuthorityChangedDomainEvent>
{
    private readonly IKnowledgeCache _cache;

    public AuthorityCacheInvalidationHandler(IKnowledgeCache cache) => _cache = cache;

    protected override Task HandleAsync(
        IssuingAuthorityChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _cache.InvalidateAsync(
            IntelligenceCacheKeys.CountryScope(domainEvent.CountryCode), cancellationToken);
    }
}
