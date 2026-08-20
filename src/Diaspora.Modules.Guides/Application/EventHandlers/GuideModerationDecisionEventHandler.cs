using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

// The kernel's legacy identifier bag also declares a GuideId. The module-local identifier is the
// authoritative one for B8; the alias resolves the collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// A moderator decided something about a guide, in Trust's queue. Applies it to the guide.
/// <para>
/// <b>Moderation happens where moderators work, and its outcome lands here.</b> B8 runs no
/// moderation queue of its own — reports route to Community, decisions are recorded by Trust —
/// and this handler is the whole of what B8 does about that: it takes a decision somebody else
/// made and applies it to the row only B8 can write.
/// </para>
/// <para>
/// <b>Filtered to guides, by content type.</b> Trust records decisions about posts, comments,
/// listings and members through the same event, and a handler that acted on all of them would
/// hide a guide because a comment identifier happened to collide. An unrecognised content type
/// returns silently: it is the common case in a healthy system.
/// </para>
/// <para>
/// Runs out of band, in its own scope, so it commits its own write. Exceptions are not swallowed
/// — an unwritable moderation decision must be retried and eventually dead-lettered, which is the
/// pipeline's job and not this handler's.
/// </para>
/// </summary>
public sealed class GuideModerationDecisionEventHandler
    : IntegrationEventHandler<ModerationDecisionRecordedIntegrationEvent>
{
    /// <summary>Trust's content-type name for a guide.</summary>
    private const string GuideContentType = "Guide";

    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GuideModerationDecisionEventHandler> _logger;

    public GuideModerationDecisionEventHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        ILogger<GuideModerationDecisionEventHandler> logger)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        ModerationDecisionRecordedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!string.Equals(
                integrationEvent.ContentType.Trim(),
                GuideContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Guid.TryParse(integrationEvent.ContentId, out var contentId))
        {
            _logger.LogWarning(
                "A guide moderation decision carried an unparseable content id '{ContentId}'.",
                integrationEvent.ContentId);

            return;
        }

        var guide = await _guides.GetAsync(GuideId.From(contentId), cancellationToken);

        if (guide is null)
        {
            // The guide was deleted between the decision and its delivery. Nothing to apply, and
            // nothing wrong: a moderator acting on content its author then removed is ordinary.
            return;
        }

        var applied = Apply(guide, integrationEvent);

        if (applied is null)
        {
            // An action that says nothing about a guide's lifecycle — escalation, a note. Trust
            // has recorded it; there is nothing for B8 to do.
            return;
        }

        if (applied.IsFailure)
        {
            _logger.LogWarning(
                "Applying moderation action {Action} to guide {GuideId} failed: {Error}.",
                integrationEvent.Action,
                guide.Id.Value,
                applied.Error.Code);

            return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Maps Trust's action vocabulary onto the aggregate's moderation mutators.
    /// <para>
    /// Null means "not a lifecycle action". Hiding and unhiding are reversible and leave the
    /// guide's own state untouched, which is what makes lifting a decision restore exactly what
    /// was there; archiving is the heavier outcome, for advice that should not come back.
    /// </para>
    /// </summary>
    private static Result? Apply(
        Guide guide, ModerationDecisionRecordedIntegrationEvent integrationEvent)
    {
        var moderatorId = UserId.From(integrationEvent.ModeratorId);

        return integrationEvent.Action.Trim().ToUpperInvariant() switch
        {
            "HIDE" or "REMOVE" or "SUSPEND" => guide.Hide(
                integrationEvent.Reason, moderatorId, integrationEvent.DecidedAt),

            "RESTORE" or "APPROVE" or "REINSTATE" => guide.Unhide(moderatorId),

            "ARCHIVE" or "REJECT" => guide.Archive(integrationEvent.DecidedAt),

            _ => null
        };
    }
}
