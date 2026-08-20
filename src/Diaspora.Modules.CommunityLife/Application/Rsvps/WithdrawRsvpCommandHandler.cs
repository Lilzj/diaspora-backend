using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Application.Rsvps;

/// <summary>
/// Withdraws the caller from an event.
/// <para>
/// <b>The waiting list is promoted here, in the same transaction that freed the place.</b> That is
/// the whole reason this is not two commands: a place that is free for the length of a separate
/// request is a place two people can be told they have, and the second of them finds out at the
/// door. The promotion runs after the release so the freed capacity is already reflected.
/// </para>
/// <para>
/// <b>Promoted in creation order, and that ordering is the feature.</b> Promoting whoever the
/// database happened to return first would make a waiting list a lottery, which is exactly what a
/// waiting list exists not to be.
/// </para>
/// <para>
/// <b>Only whole parties are promoted.</b> A family of four is not split across a queue to fill a
/// gap of two; the next entry that fits is taken, and one that does not stays where it is rather
/// than being partially admitted.
/// </para>
/// </summary>
public sealed class WithdrawRsvpCommandHandler
    : ICommandHandler<WithdrawRsvpCommand, RsvpResponse>
{
    private const int PromotionScanSize = 50;

    private readonly ICommunityEventRepository _events;
    private readonly IEventRsvpRepository _rsvps;
    private readonly IEventAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public WithdrawRsvpCommandHandler(
        ICommunityEventRepository events,
        IEventRsvpRepository rsvps,
        IEventAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IOptions<CommunityLifeOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _events = events;
        _rsvps = rsvps;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<RsvpResponse>> Handle(
        WithdrawRsvpCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<RsvpResponse>(caller.Error);
        }

        var eventId = CommunityEventId.From(command.EventId);

        var rsvp = await _rsvps.FindAsync(eventId, caller.Value, cancellationToken);

        if (rsvp is null)
        {
            return Result.Failure<RsvpResponse>(CommunityLifeErrors.RsvpNotFound);
        }

        var now = _clock.UtcNow;
        var heldPlaces = rsvp.HeldPlaces;

        var withdrawn = rsvp.Withdraw(
            _guids.NewSequentialGuid(), now, command.Request.Reason);

        if (withdrawn.IsFailure)
        {
            return Result.Failure<RsvpResponse>(withdrawn.Error);
        }

        _rsvps.Update(rsvp);

        var analytics = await _analytics.FindByEventAsync(eventId, cancellationToken);

        analytics?.RecordResponse(now, cancelled: 1);

        if (heldPlaces > 0)
        {
            await PromoteAsync(eventId, heldPlaces, now, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        CommunityLifeTelemetry.RsvpsWithdrawn.Add(1);

        return Result.Success(CommunityEventMapper.ToResponse(rsvp));
    }

    /// <summary>
    /// Fills the freed places from the waiting list, oldest first.
    /// <para>
    /// The freed count is passed in rather than re-read: the release above has not been flushed yet,
    /// so the event's counters still show the old figure. Working from what was actually released is
    /// what makes this correct without a second round trip.
    /// </para>
    /// </summary>
    private async Task PromoteAsync(
        CommunityEventId eventId,
        int freedPlaces,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!_options.Value.EnableWaitlist)
        {
            return;
        }

        var communityEvent = await _events.GetAsync(eventId, cancellationToken);

        if (communityEvent is null || communityEvent.Capacity.Maximum is null)
        {
            return;
        }

        var waiting = await _rsvps.FindWaitlistAsync(
            eventId, PromotionScanSize, cancellationToken);

        var remaining = freedPlaces;

        foreach (var candidate in waiting)
        {
            if (remaining <= 0)
            {
                return;
            }

            // Whole parties only. See the class summary.
            if (candidate.PartySize > remaining)
            {
                continue;
            }

            var promoted = candidate.Promote(_guids.NewSequentialGuid(), now);

            if (promoted.IsFailure)
            {
                continue;
            }

            _rsvps.Update(candidate);
            remaining -= candidate.PartySize;
        }
    }
}
