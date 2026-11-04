using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Analytics;
using Diaspora.Modules.Guides.Domain.Guides;
using Microsoft.Extensions.Logging;

// The kernel's legacy identifier bag also declares a GuideId. The module-local identifier is the
// authoritative one for B8; the alias resolves the collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Keeps a guide's "saved" count in step with Community, which owns the saves.
/// <para>
/// <b>Seen on the emulator, 4 Nov 2026:</b> a guide read "Saved" beside "0 saved". Community
/// announced every save and nothing here listened, so the count on the guide, the one cards,
/// trending and author stats read, never moved.
/// </para>
/// <para>
/// <b>Recounted, never incremented</b>, like the comment count: delivery is at-least-once, so a
/// +1 per message would climb with every redelivery. A null answer (Community unreachable) leaves
/// the stored count alone rather than writing a zero back.
/// </para>
/// </summary>
public sealed class GuideSavedCount
{
    private readonly IGuideRepository _guides;
    private readonly IGuideAnalyticsRepository _analytics;
    private readonly GuideBookmarkService _bookmarks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GuideSavedCount> _logger;

    public GuideSavedCount(
        IGuideRepository guides,
        IGuideAnalyticsRepository analytics,
        GuideBookmarkService bookmarks,
        IUnitOfWork unitOfWork,
        ILogger<GuideSavedCount> logger)
    {
        _guides = guides;
        _analytics = analytics;
        _bookmarks = bookmarks;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>Recounts the saves on the content, when the content is a guide.</summary>
    public async Task RecountAsync(
        string contentType, Guid contentId, CancellationToken cancellationToken)
    {
        // Community raises saves for posts, listings, events and guides alike. Filtering on the
        // content type first means the common case costs a string comparison rather than a read.
        if (!string.Equals(
                contentType.Trim(),
                nameof(CommunityContentType.Guide),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var guideId = GuideId.From(contentId);
        var guide = await _guides.GetAsync(guideId, cancellationToken);

        if (guide is null)
        {
            return;
        }

        if (await _bookmarks.CountAsync(guideId, cancellationToken) is not { } count)
        {
            _logger.LogWarning(
                "Community could not count saves for guide {GuideId}; the stored count is unchanged.",
                guideId.Value);

            return;
        }

        var analytics = await _analytics.GetByGuideAsync(guideId, cancellationToken);

        GuideEngagementService.ApplyBookmarkCount(guide, analytics, count);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Somebody saved something; if it was a guide, its saved count is recounted.</summary>
public sealed class GuideBookmarkAddedEventHandler
    : IntegrationEventHandler<CommunityBookmarkAddedIntegrationEvent>
{
    private readonly GuideSavedCount _savedCount;

    public GuideBookmarkAddedEventHandler(GuideSavedCount savedCount) => _savedCount = savedCount;

    protected override Task HandleAsync(
        CommunityBookmarkAddedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _savedCount.RecountAsync(
            integrationEvent.ContentType, integrationEvent.ContentId, cancellationToken);
    }
}

/// <summary>Somebody took a save back; if it was a guide, its saved count is recounted.</summary>
public sealed class GuideBookmarkRemovedEventHandler
    : IntegrationEventHandler<CommunityBookmarkRemovedIntegrationEvent>
{
    private readonly GuideSavedCount _savedCount;

    public GuideBookmarkRemovedEventHandler(GuideSavedCount savedCount) => _savedCount = savedCount;

    protected override Task HandleAsync(
        CommunityBookmarkRemovedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _savedCount.RecountAsync(
            integrationEvent.ContentType, integrationEvent.ContentId, cancellationToken);
    }
}
