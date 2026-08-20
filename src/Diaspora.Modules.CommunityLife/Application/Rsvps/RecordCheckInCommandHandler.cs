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
using Diaspora.Modules.CommunityLife.Extensibility.CheckIn;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Rsvps;

/// <summary>
/// Records somebody arriving at an event.
/// <para>
/// <b>Who is at the door is resolved through the check-in seam, not decided here.</b> A deployment
/// may scan a code, take a name, or let attendees confirm themselves; this handler asks the
/// registered methods which response the arrival corresponds to and records it. A method that throws
/// is skipped by the dispatcher — a failing scanner should fall through to somebody typing a name,
/// not stop the queue.
/// </para>
/// <para>
/// <b>A volunteer can work a door without being able to edit anything.</b> That is the whole reason
/// the role sits between member and moderator, and why this uses the check-in permission rather than
/// the write one.
/// </para>
/// <para>
/// <b>A party that arrives smaller is resized before it is checked in.</b> Three of a family of five
/// turning up is the ordinary case at any community event, and recording five would leave the
/// organizer counting chairs nobody sat in.
/// </para>
/// </summary>
public sealed class RecordCheckInCommandHandler
    : ICommandHandler<RecordCheckInCommand, RsvpResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IEventRsvpRepository _rsvps;
    private readonly IEventAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeCheckInDispatcher _dispatcher;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RecordCheckInCommandHandler(
        ICommunityEventRepository events,
        IEventRsvpRepository rsvps,
        IEventAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeCheckInDispatcher dispatcher,
        IGuidGenerator guids,
        IClock clock)
    {
        _events = events;
        _rsvps = rsvps;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _dispatcher = dispatcher;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<RsvpResponse>> Handle(
        RecordCheckInCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var eventId = CommunityEventId.From(command.EventId);

        var communityEvent = await _events.GetAsync(eventId, cancellationToken);

        if (communityEvent is null)
        {
            return Result.Failure<RsvpResponse>(CommunityLifeErrors.EventNotFound);
        }

        var caller = await _authorization.RequireCheckInAsync(
            communityEvent, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<RsvpResponse>(caller.Error);
        }

        var now = _clock.UtcNow;

        if (!communityEvent.AcceptsCheckInAt(now))
        {
            return Result.Failure<RsvpResponse>(CommunityLifeErrors.CheckInNotOpen);
        }

        var method = CommunityLifeSharedMapper.ParseEnum<CheckInMethod>(
            command.Request.Method);

        var resolved = await ResolveAsync(command, caller.Value, method, now, cancellationToken);

        if (resolved.IsFailure)
        {
            return Result.Failure<RsvpResponse>(resolved.Error);
        }

        var rsvp = resolved.Value;

        var resized = ResizeParty(rsvp, command.Request.ArrivedPartySize, now);

        if (resized.IsFailure)
        {
            return Result.Failure<RsvpResponse>(resized.Error);
        }

        var checkedIn = rsvp.CheckIn(
            method ?? CheckInMethod.Manual,
            _guids.NewSequentialGuid(),
            now,
            caller.Value);

        if (checkedIn.IsFailure)
        {
            return Result.Failure<RsvpResponse>(checkedIn.Error);
        }

        _rsvps.Update(rsvp);

        var analytics = await _analytics.FindByEventAsync(eventId, cancellationToken);

        analytics?.RecordResponse(now, checkedIn: 1);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        CommunityLifeTelemetry.CheckIns.Add(
            1,
            new KeyValuePair<string, object?>(
                "method", (method ?? CheckInMethod.Manual).ToString()));

        return Result.Success(CommunityEventMapper.ToResponse(rsvp));
    }

    /// <summary>
    /// Finds the response the arrival belongs to.
    /// <para>
    /// The seam is asked first. Where no method resolves it — a deployment with none registered, or
    /// a token nothing recognises — the request's own attendee or response identifier is used, which
    /// is what makes a plain "tick this person off" list work with no extensions installed at all.
    /// </para>
    /// </summary>
    private async Task<Result<EventRsvp>> ResolveAsync(
        RecordCheckInCommand command,
        UserId caller,
        CheckInMethod? preferred,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var outcome = await _dispatcher.ResolveAsync(
            new Extensibility.CheckIn.CheckInRequest
            {
                EventId = command.EventId,
                AttendeeUserId = command.Request.AttendeeUserId,
                RsvpId = command.Request.RsvpId,
                Token = command.Request.Token,
                RecordedByUserId = caller.Value,
                ArrivedPartySize = command.Request.ArrivedPartySize,
                OccurredAt = now
            },
            preferred,
            cancellationToken);

        var rsvpId = outcome.IsResolved ? outcome.RsvpId : command.Request.RsvpId;

        if (rsvpId is { } id && id != Guid.Empty)
        {
            var byId = await _rsvps.GetAsync(EventRsvpId.From(id), cancellationToken);

            return byId is null || byId.EventId.Value != command.EventId
                ? Result.Failure<EventRsvp>(CommunityLifeErrors.RsvpNotFound)
                : Result.Success(byId);
        }

        var attendeeId = outcome.IsResolved
            ? outcome.AttendeeUserId
            : command.Request.AttendeeUserId;

        if (attendeeId is not { } attendee || attendee == Guid.Empty)
        {
            return Result.Failure<EventRsvp>(CommunityLifeErrors.RsvpNotFound);
        }

        var found = await _rsvps.FindAsync(
            CommunityEventId.From(command.EventId), UserId.From(attendee), cancellationToken);

        return found is null
            ? Result.Failure<EventRsvp>(CommunityLifeErrors.RsvpNotFound)
            : Result.Success(found);
    }

    /// <summary>Applies a party that turned up smaller than it said. See the class summary.</summary>
    private static Result ResizeParty(EventRsvp rsvp, int? arrived, DateTimeOffset now)
    {
        if (arrived is not { } size || size < 1 || size == rsvp.PartySize)
        {
            return Result.Success();
        }

        var resized = rsvp.ChangePartySize(size, now);

        return resized.IsFailure ? Result.Failure(resized.Error) : Result.Success();
    }
}
