using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Moderation;

/// <summary>
/// A community event, as a moderation case sees it (7 Nov 2026).
/// <para>
/// <b>The only way staff change a member's event now.</b> The owner's routes refuse staff, and the
/// module's own moderation and report-resolution routes are gone: a decision is made on the Trust
/// case and carried out here, in the same transaction, with the real moderator recorded (the
/// Community hook this replaces recorded nobody, and hid with a stock sentence instead of the
/// moderator's note).
/// </para>
/// <para>
/// A hide pending review is the event's Hidden state, which its organiser can fix and ask about; a
/// removal is Suspended, which bars republication until staff lift it. <b>Neither cancels or
/// archives the event</b>: a cancellation tells every attendee the organiser called it off and
/// reverses the points it earned, and neither is true of a moderation decision.
/// </para>
/// </summary>
public sealed class CommunityEventModerationTarget : IModerationTarget
{
    private readonly ICommunityEventRepository _events;
    private readonly IClock _clock;

    public CommunityEventModerationTarget(ICommunityEventRepository events, IClock clock)
    {
        _events = events;
        _clock = clock;
    }

    public string ContentType => ModerationContentTypes.Event;

    public string Area => ModerationAreas.Events;

    public async Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        if (contentId == Guid.Empty)
        {
            return null;
        }

        var communityEvent = await _events.GetIncludingDeletedAsync(
            CommunityEventId.From(contentId), cancellationToken);

        return communityEvent is null
            ? null
            : new ModerationTargetSnapshot(
                communityEvent.Id.Value,
                CommunityLifeContentOwners.OfEvent(communityEvent).Value,
                communityEvent.Title.Value,
                communityEvent.ModerationState.ToString());
    }

    public async Task<Result<ModerationTargetChange>> ApplyAsync(
        Guid contentId,
        ModerationOutcome outcome,
        ModerationActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // Past the delete filter, as the moderation route it replaces was: a case about an event
        // its organiser has since deleted must still be decidable, or it sits in the queue forever.
        var communityEvent = await _events.GetIncludingDeletedAsync(
            CommunityEventId.From(contentId), cancellationToken);

        if (communityEvent is null)
        {
            return Result.Failure<ModerationTargetChange>(CommunityLifeErrors.EventNotFound);
        }

        var before = communityEvent.ModerationState;
        var now = _clock.UtcNow;
        UserId? moderator = actor.ModeratorUserId is { } id ? UserId.From(id) : null;

        switch (outcome)
        {
            case ModerationOutcome.HidePendingReview:
                // A removal that stands is not softened into a hide by a later case.
                if (before != CommunityLifeModerationState.Suspended)
                {
                    communityEvent.Hide(actor.Note, moderator, now);
                }

                break;

            case ModerationOutcome.Remove:
                communityEvent.Suspend(actor.Note, moderator, now);
                break;

            case ModerationOutcome.Restore:
                communityEvent.Clear(moderator, now);
                break;

            case ModerationOutcome.NoAction:
                // Looked at and found to be fine: the automatic flag comes off, nothing stronger.
                if (before == CommunityLifeModerationState.Flagged)
                {
                    communityEvent.Clear(moderator, now);
                }

                break;

            default:
                return Result.Failure<ModerationTargetChange>(CommunityLifeErrors.ModerationActionInvalid);
        }

        return Result.Success(new ModerationTargetChange(
            before.ToString(), communityEvent.ModerationState.ToString()));
    }
}
