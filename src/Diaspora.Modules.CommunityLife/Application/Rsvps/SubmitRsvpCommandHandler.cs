using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
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
/// Says whether the caller is coming, and with how many people.
/// <para>
/// <b>A full event does not refuse — it waitlists.</b> Turning somebody away at the moment they said
/// yes loses them; putting them in a queue that moves when anybody withdraws keeps them, and is what
/// every organizer running a hall does anyway. A deployment can turn the waiting list off, and then
/// a full event is refused with a reason that says so.
/// </para>
/// <para>
/// <b>The party size is the unit throughout.</b> "Me and three others" is how a family answers a
/// naming ceremony, and an event counting responses would have its organizer lay out a quarter of
/// the chairs they need.
/// </para>
/// <para>
/// <b>The capacity decision is made against the counters inside the transaction.</b> Two families
/// answering at once are serialised by the event row they both settle against, which is where the
/// race is actually resolved — the check here is what turns the loser into a waitlist entry rather
/// than an error.
/// </para>
/// </summary>
public sealed class SubmitRsvpCommandHandler : ICommandHandler<SubmitRsvpCommand, RsvpResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IEventRsvpRepository _rsvps;
    private readonly IEventAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SubmitRsvpCommandHandler(
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
        SubmitRsvpCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<RsvpResponse>(caller.Error);
        }

        var communityEvent = await _events.GetAsync(
            CommunityEventId.From(command.EventId), cancellationToken);

        if (communityEvent is null)
        {
            return Result.Failure<RsvpResponse>(CommunityLifeErrors.EventNotFound);
        }

        var readable = await _authorization.RequireEventReadAsync(
            communityEvent, cancellationToken);

        if (readable.IsFailure)
        {
            return Result.Failure<RsvpResponse>(readable.Error);
        }

        var now = _clock.UtcNow;

        if (!communityEvent.AcceptsRsvpAt(now))
        {
            return Result.Failure<RsvpResponse>(CommunityLifeErrors.RsvpClosed);
        }

        if (CommunityLifeSharedMapper.ParseEnum<RsvpStatus>(command.Request.Status)
            is not { } requested
            || requested is not (RsvpStatus.Going or RsvpStatus.Interested))
        {
            return Result.Failure<RsvpResponse>(CommunityLifeErrors.RsvpTransitionInvalid);
        }

        var partySize = Math.Max(1, command.Request.PartySize);

        var status = ResolveStatus(communityEvent, requested, partySize);

        if (status.IsFailure)
        {
            return Result.Failure<RsvpResponse>(status.Error);
        }

        var existing = await _rsvps.FindAsync(
            communityEvent.Id, caller.Value, cancellationToken);

        var rsvp = existing is null
            ? await OpenAsync(communityEvent, caller, status.Value, partySize, command, now,
                cancellationToken)
            : Move(existing, status.Value, partySize, command, now);

        if (rsvp.IsFailure)
        {
            return Result.Failure<RsvpResponse>(rsvp.Error);
        }

        await RecordAnalyticsAsync(communityEvent, status.Value, now, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        CommunityLifeTelemetry.RsvpsSubmitted.Add(
            1, new KeyValuePair<string, object?>("status", status.Value.ToString()));

        // B12.1. Told at the one moment it matters: somebody who has just been waitlisted needs to
        // know whether they are second or two hundredth, and it is the difference between waiting
        // and making other plans. Read after the save, so the position counts this response's own
        // row correctly rather than the state before it existed.
        var position = rsvp.Value.Status == RsvpStatus.Waitlisted
            ? await _rsvps.CountWaitlistedAheadAsync(
                communityEvent.Id, rsvp.Value.CreatedAt, cancellationToken) + 1
            : (int?)null;

        return Result.Success(
            CommunityEventMapper.ToResponse(
                rsvp.Value, position, communityEvent.SeriesId?.Value));
    }

    /// <summary>
    /// What the response actually becomes, given how full the event is.
    /// <para>
    /// Interest never takes a place, so it is never waitlisted. A commitment that does not fit goes
    /// on the waiting list where the deployment allows one, and is refused where it does not — with
    /// the race counted, because how often a hall fills between two taps is a number worth watching.
    /// </para>
    /// </summary>
    private Result<RsvpStatus> ResolveStatus(
        CommunityEvent communityEvent, RsvpStatus requested, int partySize)
    {
        if (requested != RsvpStatus.Going)
        {
            return Result.Success(requested);
        }

        if (communityEvent.Capacity.Maximum is not { } maximum)
        {
            return Result.Success(RsvpStatus.Going);
        }

        if (communityEvent.Capacity.GoingCount + partySize <= maximum)
        {
            return Result.Success(RsvpStatus.Going);
        }

        CommunityLifeTelemetry.CapacityRacesLost.Add(1);

        return _options.Value.EnableWaitlist
            ? Result.Success(RsvpStatus.Waitlisted)
            : Result.Failure<RsvpStatus>(CommunityLifeErrors.EventFull);
    }

    private async Task<Result<EventRsvp>> OpenAsync(
        CommunityEvent communityEvent,
        Result<SharedKernel.Identity.UserId> caller,
        RsvpStatus status,
        int partySize,
        SubmitRsvpCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var live = await _rsvps.SearchAsync(
            new RsvpQueryFilter
            {
                AttendeeUserId = caller.Value,
                Statuses = [RsvpStatus.Going, RsvpStatus.Interested, RsvpStatus.Waitlisted],
                UpcomingAsOf = now,
                Limit = _options.Value.MaxLiveRsvpsPerAttendee + 1
            },
            cancellationToken);

        if (live.Items.Count >= _options.Value.MaxLiveRsvpsPerAttendee)
        {
            return Result.Failure<EventRsvp>(CommunityLifeErrors.TooManyRsvps);
        }

        var rsvp = EventRsvp.Respond(
            EventRsvpId.From(_guids.NewSequentialGuid()),
            communityEvent.Id,
            communityEvent.OrganizationId,
            caller.Value,
            status,
            partySize,
            _guids.NewSequentialGuid(),
            now,
            command.Request.Note);

        if (rsvp.IsFailure)
        {
            return rsvp;
        }

        await _rsvps.AddAsync(rsvp.Value, cancellationToken);

        return rsvp;
    }

    /// <summary>
    /// Moves a response the caller already has.
    /// <para>
    /// The party size is applied before the status, so a family growing from two to four while
    /// staying "going" still moves the counters — the size change raises its own settlement and the
    /// status change would otherwise report the old number.
    /// </para>
    /// </summary>
    private Result<EventRsvp> Move(
        EventRsvp existing,
        RsvpStatus status,
        int partySize,
        SubmitRsvpCommand command,
        DateTimeOffset now)
    {
        if (partySize != existing.PartySize)
        {
            var resized = existing.ChangePartySize(partySize, now);

            if (resized.IsFailure)
            {
                return Result.Failure<EventRsvp>(resized.Error);
            }
        }

        var moved = existing.MoveTo(status, _guids.NewSequentialGuid(), now);

        if (moved.IsFailure)
        {
            return Result.Failure<EventRsvp>(moved.Error);
        }

        existing.SetNote(command.Request.Note, now);
        _rsvps.Update(existing);

        return Result.Success(existing);
    }

    private async Task RecordAnalyticsAsync(
        CommunityEvent communityEvent,
        RsvpStatus status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var analytics = await _analytics.FindByEventAsync(communityEvent.Id, cancellationToken);

        analytics?.RecordResponse(
            now,
            rsvps: 1,
            interested: status == RsvpStatus.Interested ? 1 : 0,
            going: status == RsvpStatus.Going ? 1 : 0,
            waitlisted: status == RsvpStatus.Waitlisted ? 1 : 0);
    }
}
