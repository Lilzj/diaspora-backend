using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Guides;
using Microsoft.Extensions.Logging;

// The kernel's legacy identifier bag also declares a GuideId. The module-local identifier is the
// authoritative one for B8; the alias resolves the collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Somebody commented on a guide. Refreshes the rollup and republishes the guide-shaped fact.
/// <para>
/// <b>Community owns the comment; B8 owns the number on the card.</b> Rendering a guide's comment
/// count by calling Community on every card of every discovery page would be a cross-module call
/// per row, so the count is denormalised onto the guide's engagement — and this handler is what
/// keeps it honest.
/// </para>
/// <para>
/// <b>Recounted, never incremented.</b> Delivery is at-least-once, so a <c>+1</c> per message
/// drifts upward every time one is redelivered; asking Community for the authoritative count and
/// replacing the stored value cannot drift. A null answer — Community unreachable — leaves the
/// stored count alone rather than writing a zero back, which is the failure that would empty
/// every comment count on the platform during one outage.
/// </para>
/// <para>
/// <b>The republish is the point of the second half.</b> A consumer interested in guides should
/// not have to subscribe to every comment on the platform and filter it, so the guide-shaped fact
/// is announced on B8's own contract.
/// </para>
/// </summary>
public sealed class GuideCommentAddedEventHandler
    : IntegrationEventHandler<CommunityCommentAddedIntegrationEvent>
{
    private readonly IGuideRepository _guides;
    private readonly GuideDiscussionService _discussion;
    private readonly GuideIntegrationEventRelay _relay;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GuideCommentAddedEventHandler> _logger;

    public GuideCommentAddedEventHandler(
        IGuideRepository guides,
        GuideDiscussionService discussion,
        GuideIntegrationEventRelay relay,
        IUnitOfWork unitOfWork,
        ILogger<GuideCommentAddedEventHandler> logger)
    {
        _guides = guides;
        _discussion = discussion;
        _relay = relay;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        CommunityCommentAddedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Community raises this for posts, listings, events and guides alike. Filtering on the
        // content type first means the common case costs a string comparison rather than a read.
        if (!string.Equals(
                integrationEvent.ContentType.Trim(),
                nameof(CommunityContentType.Guide),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var guideId = GuideId.From(integrationEvent.ContentId);
        var guide = await _guides.GetAsync(guideId, cancellationToken);

        if (guide is null)
        {
            // A comment on a guide that has since been deleted. Community will stop accepting
            // them once its content source reports the guide gone; nothing to do here.
            return;
        }

        var count = await _discussion.CountAsync(guideId, cancellationToken);

        if (count is { } authoritative)
        {
            guide.ApplyCommentCount(authoritative);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "Community could not count comments for guide {GuideId}; the stored rollup is unchanged.",
                guideId.Value);
        }

        await _relay.PublishCommentAddedAsync(
            guideId.Value,
            integrationEvent.CommentId,
            integrationEvent.AuthorId,
            integrationEvent.OccurredAt,
            cancellationToken);
    }
}
