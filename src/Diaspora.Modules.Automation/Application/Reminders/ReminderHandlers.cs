using Diaspora.Application.Abstractions;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Application.Mapping;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Contracts.V1.Requests;
using Diaspora.Modules.Automation.Contracts.V1.Responses;
using Diaspora.Modules.Automation.Domain.Errors;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Automation.Application.Reminders;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-008 — reminders, with the five states §12 requires to stay distinct.
//
// `configured` is what the member did; `scheduled` is what the platform accepted. In this deployment
// both happen in the same request, but the states stay separate because the day the two diverge — a
// subject the platform cannot see, a zone it cannot resolve — the difference is the whole answer.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Every reminder this member has on one subject. Self only.</summary>
[RequirePolicy(AutomationPolicies.Read)]
public sealed record ListRemindersQuery : IQuery<IReadOnlyList<ReminderResponse>>
{
    public required string SubjectKind { get; init; }

    public required Guid SubjectId { get; init; }
}

public sealed class ListRemindersQueryHandler
    : IQueryHandler<ListRemindersQuery, IReadOnlyList<ReminderResponse>>
{
    private readonly IAutomationReminderRepository _reminders;
    private readonly AutomationAccessService _access;

    public ListRemindersQueryHandler(
        IAutomationReminderRepository reminders, AutomationAccessService access)
    {
        _reminders = reminders;
        _access = access;
    }

    public async Task<Result<IReadOnlyList<ReminderResponse>>> Handle(
        ListRemindersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ReminderResponse>>(member.Error);
        }

        // The member is the token's. There is no read-another-member's-reminders variant, because a
        // reminder is a statement that somebody intends to attend something.
        var rows = await _reminders.ListForSubjectAsync(
            member.Value, query.SubjectKind, query.SubjectId, cancellationToken);

        return Result.Success<IReadOnlyList<ReminderResponse>>(
            [.. rows.Select(AutomationMapper.ToResponse)]);
    }
}

/// <summary>Sets a reminder. Self only, and mandatorily idempotent.</summary>
[RequirePolicy(AutomationPolicies.RemindersWrite)]
public sealed record CreateReminderCommand : ICommand<ReminderResponse>
{
    public required CreateReminderRequest Request { get; init; }
}

public sealed class CreateReminderCommandHandler
    : ICommandHandler<CreateReminderCommand, ReminderResponse>
{
    private readonly IAutomationReminderRepository _reminders;
    private readonly AutomationAccessService _access;
    private readonly IClock _clock;

    public CreateReminderCommandHandler(
        IAutomationReminderRepository reminders,
        AutomationAccessService access,
        IClock clock)
    {
        _reminders = reminders;
        _access = access;
        _clock = clock;
    }

    public async Task<Result<ReminderResponse>> Handle(
        CreateReminderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<ReminderResponse>(member.Error);
        }

        if (request.SubjectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.SubjectKind) ||
            request.SubjectMoment == default)
        {
            return Result.Failure<ReminderResponse>(AutomationErrors.ReminderSubjectUnknown());
        }

        if (request.OffsetMinutes is < 0 or > 43_200)
        {
            return Result.Failure<ReminderResponse>(
                AutomationErrors.InvalidSchedule(
                    "A reminder can be set from the moment itself up to thirty days beforehand."));
        }

        // The subject's own zone, never the device's. It is stored because re-deriving it later from a
        // device would give a different answer if the member travelled.
        var zone = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "UTC" : request.TimeZoneId;

        if (!AutomationScheduleCalculator.IsKnownTimeZone(zone))
        {
            return Result.Failure<ReminderResponse>(AutomationErrors.UnknownTimeZone(zone));
        }

        var anchor = string.Equals(request.Anchor, "end", StringComparison.OrdinalIgnoreCase)
            ? "end"
            : "start";

        var key = AutomationReminder.BuildKey(
            member.Value, request.SubjectKind, request.SubjectId, request.OffsetMinutes, anchor);

        // The mandatory idempotency on (member, subject, offset, anchor). Tapping "remind me" twice
        // must produce one reminder: a duplicate is two identical pushes a minute apart, which is the
        // most common way an automation feature loses a member's trust.
        var existing = await _reminders.FindByKeyAsync(key, cancellationToken);

        if (existing is not null && existing.IsPending)
        {
            return Result.Success(AutomationMapper.ToResponse(existing));
        }

        var now = _clock.UtcNow;
        var dueAt = request.SubjectMoment.AddMinutes(-request.OffsetMinutes);

        if (dueAt <= now)
        {
            return Result.Failure<ReminderResponse>(AutomationErrors.ReminderInPast());
        }

        if (existing is not null)
        {
            // A cancelled or sent reminder on the same key is reinstated rather than duplicated: the
            // unique index would refuse a second row, and answering 409 for "remind me again about
            // next month's occurrence" would be wrong.
            existing.Reschedule(request.SubjectMoment, dueAt);
            await _reminders.SaveChangesAsync(cancellationToken);

            return Result.Success(AutomationMapper.ToResponse(existing));
        }

        var reminder = AutomationReminder.Configure(
            member.Value,
            request.SubjectKind,
            request.SubjectId,
            request.SubjectTitle ?? "An event",
            request.SubjectMoment,
            request.OffsetMinutes,
            anchor,
            zone,
            now);

        reminder.MarkScheduled(dueAt);

        _reminders.Add(reminder);
        await _reminders.SaveChangesAsync(cancellationToken);

        return Result.Success(AutomationMapper.ToResponse(reminder));
    }
}

/// <summary>
/// Cancels a reminder.
/// <para>
/// Idempotent: cancelling a cancelled reminder answers 200 with the same body.
/// </para>
/// </summary>
[RequirePolicy(AutomationPolicies.RemindersWrite)]
public sealed record CancelReminderCommand : ICommand<ReminderResponse>
{
    public required Guid ReminderId { get; init; }
}

public sealed class CancelReminderCommandHandler
    : ICommandHandler<CancelReminderCommand, ReminderResponse>
{
    private readonly IAutomationReminderRepository _reminders;
    private readonly AutomationAccessService _access;

    public CancelReminderCommandHandler(
        IAutomationReminderRepository reminders, AutomationAccessService access)
    {
        _reminders = reminders;
        _access = access;
    }

    public async Task<Result<ReminderResponse>> Handle(
        CancelReminderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<ReminderResponse>(member.Error);
        }

        var reminder = await _reminders.GetAsync(
            AutomationReminderId.From(command.ReminderId), cancellationToken);

        // Not-found for both "does not exist" and "not yours". A 403 on somebody else's reminder id
        // confirms that the id exists, and a reminder discloses that a member intends to attend
        // something.
        if (reminder is null || reminder.MemberId != member.Value)
        {
            return Result.Failure<ReminderResponse>(AutomationErrors.NotFound());
        }

        reminder.Cancel();
        await _reminders.SaveChangesAsync(cancellationToken);

        return Result.Success(AutomationMapper.ToResponse(reminder));
    }
}
