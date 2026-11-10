using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Integrations.Application.Mapping;
using Diaspora.Modules.Integrations.Application.Services;
using Diaspora.Modules.Integrations.Contracts.V1.Responses;
using Diaspora.Modules.Integrations.Domain.Calendar;
using Diaspora.Modules.Integrations.Domain.Errors;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Modules.Integrations.Extensibility.Providers;
using Diaspora.Modules.Integrations.Infrastructure.Providers;
using Diaspora.Modules.Integrations.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Integrations.Application.Calendar;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE16-GAP-003 and GAP-004 — the proxied calendar boundary, and PHASE18-GAP-025's
// integration half.
//
// Why a proxy at all, in the register's own words: "a device that could list calendars directly
// would be a device holding a Google access token."
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The calendars a connected account can see.
/// <para>
/// <b>The response is never logged and never cached.</b> Calendar titles are personal — "Therapy",
/// "Dad's chemo" — and the register singles this out as "the one that is easy to miss". No cache
/// entry is written for this query anywhere in the module, which is a deliberate cost: the call goes
/// to the provider every time somebody opens the picker.
/// </para>
/// </summary>
[Authorize(Policy = IntegrationsPolicies.CalendarRead)]
public sealed record ListRemoteCalendarsQuery : IQuery<IReadOnlyList<CalendarRefResponse>>
{
    public required string Provider { get; init; }
}

public sealed class ListRemoteCalendarsQueryHandler
    : IQueryHandler<ListRemoteCalendarsQuery, IReadOnlyList<CalendarRefResponse>>
{
    private readonly IntegrationAccessService _access;
    private readonly IntegrationProviderRegistry _registry;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationContext _correlation;

    public ListRemoteCalendarsQueryHandler(
        IntegrationAccessService access,
        IntegrationProviderRegistry registry,
        ICurrentUser currentUser,
        ICorrelationContext correlation)
    {
        _access = access;
        _registry = registry;
        _currentUser = currentUser;
        _correlation = correlation;
    }

    public async Task<Result<IReadOnlyList<CalendarRefResponse>>> Handle(
        ListRemoteCalendarsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var context = await CalendarContext.ResolveAsync(
            query.Provider, _currentUser, _access, _registry, cancellationToken);

        if (context.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CalendarRefResponse>>(context.Error);
        }

        var (provider, adapter, access) = context.Value;

        var outcome = await adapter.ListCalendarsAsync(access.AccessToken, cancellationToken);

        await _access.ObserveAsync(
            access.Connection, outcome, _correlation.CorrelationId, cancellationToken);

        if (!outcome.Succeeded || outcome.Value is null)
        {
            return Result.Failure<IReadOnlyList<CalendarRefResponse>>(
                IntegrationErrors.FromProvider(outcome, "listing your calendars"));
        }

        return Result.Success<IReadOnlyList<CalendarRefResponse>>(
            [.. outcome.Value.Select(calendar => IntegrationMapper.ToCalendarRef(calendar, provider.Value))]);
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Writes an entry into a connected calendar.
/// <para>
/// <b>Three barriers stop a retry becoming a duplicate, and the order they are checked in is the
/// design.</b>
/// </para>
/// <list type="number">
/// <item><b>The idempotency key.</b> The same key against the same source returns the recorded
/// outcome without calling the provider. This is what makes a retry after a timeout safe, and the
/// register calls a duplicate entry "the most visible possible failure of this integration".</item>
/// <item><b>The content fingerprint.</b> An unchanged event already written is a no-op, reported as
/// <c>wasDeduplicated</c>. This is what makes a nightly re-export cheap instead of a rewrite of every
/// entry.</item>
/// <item><b>The link row.</b> A changed event with an existing link is an <em>update</em> to the
/// entry the provider already holds, not a second create. Without it the honest behaviour would be
/// two entries for one event, which is what happens when a mapping is not persisted.</item>
/// </list>
/// <para>
/// <b>The calendar's write permission is checked against the provider, not against the request.</b>
/// §46: "a calendar id is a NAME, not a capability — the server re-derives access from the
/// connection." A read-only calendar is a 403 rather than a silent no-op.
/// </para>
/// </summary>
[Transactional(Reason =
    "Writes at the provider and records the source-to-external mapping. The mapping is the "
    + "duplicate barrier, so committing the write without it would make the next retry a second "
    + "entry in somebody's calendar.")]
[Authorize(Policy = IntegrationsPolicies.CalendarWrite)]
public sealed record WriteCalendarEventCommand : ICommand<CalendarEventRefResponse>
{
    public required string Provider { get; init; }

    public string? CalendarId { get; init; }

    public string? SourceId { get; init; }

    public string? SourceKind { get; init; }

    public string? Title { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public string? TimeZone { get; init; }

    public bool IsAllDay { get; init; }

    public string? Location { get; init; }

    public string? Notes { get; init; }

    public string? Url { get; init; }

    public IReadOnlyList<int> ReminderMinutes { get; init; } = [];

    public string? RecurrenceRule { get; init; }

    /// <summary>The <c>Idempotency-Key</c> header. Mandatory in the register; enforced here.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// The external entry to replace.
    /// <para>
    /// Set by the <c>PUT</c> route. Absent on <c>POST</c>, where the link row decides whether the
    /// write is a create or an update.
    /// </para>
    /// </summary>
    public string? ExternalEventId { get; init; }
}

public sealed class WriteCalendarEventCommandHandler
    : ICommandHandler<WriteCalendarEventCommand, CalendarEventRefResponse>
{
    private readonly IntegrationAccessService _access;
    private readonly IntegrationProviderRegistry _registry;
    private readonly ICalendarEventLinkRepository _links;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationContext _correlation;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly ILogger<WriteCalendarEventCommandHandler> _logger;

    public WriteCalendarEventCommandHandler(
        IntegrationAccessService access,
        IntegrationProviderRegistry registry,
        ICalendarEventLinkRepository links,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ICorrelationContext correlation,
        IGuidGenerator guids,
        IClock clock,
        ILogger<WriteCalendarEventCommandHandler> logger)
    {
        _access = access;
        _registry = registry;
        _links = links;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _correlation = correlation;
        _guids = guids;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<CalendarEventRefResponse>> Handle(
        WriteCalendarEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validated = Validate(command);

        if (validated.IsFailure)
        {
            return Result.Failure<CalendarEventRefResponse>(validated.Error);
        }

        var context = await CalendarContext.ResolveAsync(
            command.Provider, _currentUser, _access, _registry, cancellationToken);

        if (context.IsFailure)
        {
            return Result.Failure<CalendarEventRefResponse>(context.Error);
        }

        var (provider, adapter, access) = context.Value;

        if (!adapter.Capabilities.Contains(ExternalCalendarCapability.CreateEvent))
        {
            return Result.Failure<CalendarEventRefResponse>(
                IntegrationErrors.CapabilityUnsupported("writing calendar entries"));
        }

        var calendarId = command.CalendarId!.Trim();

        // The calendar is resolved from the provider, not trusted from the request. §46.
        var permitted = await EnsureWritableAsync(adapter, access, calendarId, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<CalendarEventRefResponse>(permitted.Error);
        }

        var fingerprint = CalendarEventLink.Fingerprint(
            command.Title!,
            command.StartsAt!.Value,
            command.EndsAt!.Value,
            command.TimeZone!,
            command.IsAllDay,
            command.Location,
            command.Notes,
            command.Url,
            command.ReminderMinutes,
            command.RecurrenceRule);

        var existing = await _links.FindAsync(
            access.Connection.Id, calendarId, command.SourceId!.Trim(), cancellationToken);

        // Barrier one: the same attempt, replayed.
        if (existing is not null && existing.WasWrittenBy(command.IdempotencyKey))
        {
            IntegrationsTelemetry.CalendarWritesDeduplicated.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider.Value),
                new KeyValuePair<string, object?>("reason", "idempotency_key"));

            return Result.Success(IntegrationMapper.ToEventRef(existing, provider.Value, true));
        }

        // Barrier two: an unchanged event already written.
        if (existing is not null && existing.Matches(fingerprint))
        {
            existing.RecordAttempt(_clock.UtcNow);
            _links.Update(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            IntegrationsTelemetry.CalendarWritesDeduplicated.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider.Value),
                new KeyValuePair<string, object?>("reason", "unchanged"));

            return Result.Success(IntegrationMapper.ToEventRef(existing, provider.Value, true));
        }

        var draft = new ExternalCalendarEventDraft
        {
            Title = command.Title!,
            StartsAt = command.StartsAt!.Value,
            EndsAt = command.EndsAt!.Value,
            TimeZone = command.TimeZone!,
            IsAllDay = command.IsAllDay,
            Location = command.Location,
            Notes = command.Notes,
            Url = command.Url,
            ReminderMinutes = command.ReminderMinutes,
            RecurrenceRule = command.RecurrenceRule,
            IdempotencyKey = command.IdempotencyKey
        };

        // Barrier three: an existing mapping makes this an update rather than a second create.
        var target = command.ExternalEventId?.Trim() is { Length: > 0 } explicitTarget
            ? explicitTarget
            : existing?.ExternalEventId;

        var outcome = target is null
            ? await adapter.CreateEventAsync(access.AccessToken, calendarId, draft, cancellationToken)
            : await adapter.UpdateEventAsync(
                access.AccessToken, calendarId, target, draft, cancellationToken);

        await _access.ObserveAsync(
            access.Connection, outcome, _correlation.CorrelationId, cancellationToken);

        if (!outcome.Succeeded || outcome.Value is not { } written)
        {
            return Result.Failure<CalendarEventRefResponse>(
                IntegrationErrors.FromProvider(outcome, "writing this calendar entry"));
        }

        var now = _clock.UtcNow;

        if (existing is null)
        {
            existing = CalendarEventLink.Record(
                CalendarEventLinkId.From(_guids.NewSequentialGuid()),
                access.Connection.Id,
                access.Connection.OwnerId,
                calendarId,
                command.SourceKind ?? "event",
                command.SourceId!,
                written.ExternalEventId,
                command.TimeZone!,
                fingerprint,
                command.IdempotencyKey,
                written.ViewUrl,
                now);

            _links.Add(existing);
        }
        else
        {
            existing.RecordUpdate(
                written.ExternalEventId,
                command.TimeZone!,
                fingerprint,
                command.IdempotencyKey,
                written.ViewUrl,
                now);

            _links.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        IntegrationsTelemetry.CalendarWrites.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider.Value),
            new KeyValuePair<string, object?>("operation", target is null ? "create" : "update"));

        return Result.Success(IntegrationMapper.ToEventRef(existing, provider.Value, false));
    }

    /// <summary>
    /// Everything that must be true before a provider is called.
    /// <para>
    /// <b>The time zone is the one worth reading.</b> It is required, it is validated against the
    /// zone database, and neither is a formality: a missing zone silently defaults to the server's,
    /// and "a calendar entry that says 17:00 BST is a different — and wrong — claim about a
    /// real-world event".
    /// </para>
    /// </summary>
    private static Result Validate(WriteCalendarEventCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CalendarId))
        {
            return Result.Failure(Error.Validation(
                IntegrationsErrorCodes.CalendarNotFound, "A calendar must be named."));
        }

        if (string.IsNullOrWhiteSpace(command.SourceId))
        {
            return Result.Failure(Error.Validation(
                IntegrationsErrorCodes.CalendarEventNotFound,
                "A source identifier is required. Without one an entry cannot be deduplicated, and "
                + "a retry would write a second copy."));
        }

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return Result.Failure(Error.Validation(
                IntegrationsErrorCodes.CalendarEventNotFound, "A title is required."));
        }

        if (command.StartsAt is not { } startsAt || command.EndsAt is not { } endsAt)
        {
            return Result.Failure(Error.Validation(
                IntegrationsErrorCodes.CalendarEventNotFound,
                "A start and an end are required."));
        }

        if (endsAt < startsAt)
        {
            return Result.Failure(Error.Validation(
                IntegrationsErrorCodes.CalendarEventNotFound,
                "An entry cannot end before it starts."));
        }

        if (string.IsNullOrWhiteSpace(command.TimeZone))
        {
            return Result.Failure(IntegrationErrors.TimeZoneRequired());
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(command.TimeZone.Trim(), out _))
        {
            // Refused rather than substituted. A provider given an unknown zone either rejects the
            // write or silently interprets it as UTC, and the second is worse: the entry appears,
            // at the wrong time, with nothing recorded to say why.
            return Result.Failure(IntegrationErrors.TimeZoneUnknown(command.TimeZone.Trim()));
        }

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            return Result.Failure(Error.Validation(
                IntegrationsErrorCodes.CalendarEventNotFound,
                "An Idempotency-Key header is required for a calendar write. Without one a retry "
                + "after a timeout writes a second entry into your calendar."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Confirms the calendar exists on this grant and accepts changes.
    /// <para>
    /// An extra provider call before every write, and it is worth it: without it, a write to a
    /// calendar the member cannot modify fails at the provider with a message the client cannot
    /// classify, and a write to a calendar id that is not theirs is an attempt this platform did not
    /// notice making.
    /// </para>
    /// </summary>
    private async Task<Result> EnsureWritableAsync(
        IExternalCalendarProvider adapter,
        IntegrationAccess access,
        string calendarId,
        CancellationToken cancellationToken)
    {
        if (!adapter.Capabilities.Contains(ExternalCalendarCapability.List))
        {
            // An adapter that cannot enumerate cannot be asked to prove access, so the provider's
            // own refusal is the only available check. Logged, because it means this deployment is
            // running without a control the design assumes.
            _logger.LogDebug(
                "The {Provider} adapter cannot list calendars, so a write's target could not be "
                + "verified before the call.",
                adapter.Key);

            return Result.Success();
        }

        var calendars = await adapter.ListCalendarsAsync(access.AccessToken, cancellationToken);

        if (!calendars.Succeeded || calendars.Value is null)
        {
            return Result.Failure(
                IntegrationErrors.FromProvider(calendars, "checking that calendar"));
        }

        var calendar = calendars.Value.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, calendarId, StringComparison.Ordinal));

        if (calendar is null)
        {
            return Result.Failure(IntegrationErrors.CalendarNotFound(calendarId));
        }

        return calendar.AllowsModifications
            ? Result.Success()
            : Result.Failure(IntegrationErrors.CalendarReadOnly());
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Removes an entry this platform wrote.
/// <para>
/// Idempotent: "deleting an already-deleted event returns 200". An entry the provider no longer has
/// is a success — the member deleted it themselves, and a red banner for that would be wrong.
/// </para>
/// </summary>
[Transactional(Reason =
    "Deletes at the provider and marks the mapping removed. A commit of one without the other "
    + "would leave a link the next export treats as live.")]
[Authorize(Policy = IntegrationsPolicies.CalendarWrite)]
public sealed record DeleteCalendarEventCommand : ICommand
{
    public required string Provider { get; init; }

    public required string ExternalEventId { get; init; }

    /// <summary>Optional; the mapping supplies it when the client does not.</summary>
    public string? CalendarId { get; init; }
}

public sealed class DeleteCalendarEventCommandHandler : ICommandHandler<DeleteCalendarEventCommand>
{
    private readonly IntegrationAccessService _access;
    private readonly IntegrationProviderRegistry _registry;
    private readonly ICalendarEventLinkRepository _links;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;

    public DeleteCalendarEventCommandHandler(
        IntegrationAccessService access,
        IntegrationProviderRegistry registry,
        ICalendarEventLinkRepository links,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ICorrelationContext correlation,
        IClock clock)
    {
        _access = access;
        _registry = registry;
        _links = links;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _correlation = correlation;
        _clock = clock;
    }

    public async Task<Result> Handle(
        DeleteCalendarEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var context = await CalendarContext.ResolveAsync(
            command.Provider, _currentUser, _access, _registry, cancellationToken);

        if (context.IsFailure)
        {
            return Result.Failure(context.Error);
        }

        var (_, adapter, access) = context.Value;

        var link = await _links.FindByExternalEventAsync(
            access.Connection.Id, command.ExternalEventId.Trim(), cancellationToken);

        // Only entries this platform recorded writing can be deleted through it. A client that could
        // pass any external id would be a client that could delete arbitrary entries from a member's
        // calendar using a grant the member gave for export.
        if (link is null)
        {
            return Result.Success();
        }

        if (link.State is Domain.Enums.CalendarLinkState.Removed)
        {
            return Result.Success();
        }

        var outcome = await adapter.DeleteEventAsync(
            access.AccessToken, link.CalendarId, link.ExternalEventId, cancellationToken);

        await _access.ObserveAsync(
            access.Connection, outcome, _correlation.CorrelationId, cancellationToken);

        if (!outcome.Succeeded)
        {
            return Result.Failure(
                IntegrationErrors.FromProvider(outcome, "removing this calendar entry"));
        }

        link.RecordRemoval(_clock.UtcNow);
        _links.Update(link);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        IntegrationsTelemetry.CalendarWrites.Add(
            1,
            new KeyValuePair<string, object?>("provider", command.Provider),
            new KeyValuePair<string, object?>("operation", "delete"));

        return Result.Success();
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Whether the provider already holds an entry Diaspora wrote for this source.
/// <para>
/// Answered from the mapping rather than by calling the provider: the mapping is what the platform
/// knows, and a client asking "have I exported this" is asking about the platform's record. A
/// provider round-trip here would also mean a calendar read on every render of an export button.
/// </para>
/// </summary>
[Authorize(Policy = IntegrationsPolicies.CalendarRead)]
public sealed record LookupCalendarEventQuery : IQuery<CalendarEventRefResponse?>
{
    public required string Provider { get; init; }

    public required string SourceId { get; init; }

    public required string CalendarId { get; init; }
}

public sealed class LookupCalendarEventQueryHandler
    : IQueryHandler<LookupCalendarEventQuery, CalendarEventRefResponse?>
{
    private readonly IIntegrationConnectionRepository _connections;
    private readonly ICalendarEventLinkRepository _links;
    private readonly ICurrentUser _currentUser;

    public LookupCalendarEventQueryHandler(
        IIntegrationConnectionRepository connections,
        ICalendarEventLinkRepository links,
        ICurrentUser currentUser)
    {
        _connections = connections;
        _links = links;
        _currentUser = currentUser;
    }

    public async Task<Result<CalendarEventRefResponse?>> Handle(
        LookupCalendarEventQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = IntegrationCaller.From(_currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<CalendarEventRefResponse?>(caller.Error);
        }

        if (!ProviderKey.TryParse(query.Provider, out var provider))
        {
            return Result.Failure<CalendarEventRefResponse?>(
                IntegrationErrors.ProviderUnknown(query.Provider));
        }

        var connection = await _connections.FindAsync(
            caller.Value.OwnerKind, caller.Value.OwnerId, provider.Value, cancellationToken);

        if (connection is null)
        {
            return Result.Success<CalendarEventRefResponse?>(null);
        }

        var link = await _links.FindAsync(
            connection.Id, query.CalendarId.Trim(), query.SourceId.Trim(), cancellationToken);

        return Result.Success(
            link is null || link.State is not Domain.Enums.CalendarLinkState.Written
                ? null
                : IntegrationMapper.ToEventRef(link, provider.Value, false));
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The three things every calendar call needs, resolved once.
/// <para>
/// A parsed provider key, a configured adapter, and a grant belonging to the caller. Written once so
/// that four handlers cannot each get the order of the checks slightly different — in particular so
/// that none of them reaches an adapter before the connection has been proved to be the caller's.
/// </para>
/// </summary>
internal static class CalendarContext
{
    public static async Task<Result<(ProviderKey Provider, IExternalCalendarProvider Adapter, IntegrationAccess Access)>>
        ResolveAsync(
            string providerKey,
            ICurrentUser currentUser,
            IntegrationAccessService access,
            IntegrationProviderRegistry registry,
            CancellationToken cancellationToken)
    {
        var caller = IntegrationCaller.From(currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<(ProviderKey, IExternalCalendarProvider, IntegrationAccess)>(
                caller.Error);
        }

        if (!ProviderKey.TryParse(providerKey, out var provider) || !provider.IsCalendar)
        {
            return Result.Failure<(ProviderKey, IExternalCalendarProvider, IntegrationAccess)>(
                IntegrationErrors.ProviderUnknown(providerKey));
        }

        if (registry.FindCalendar(provider.Value) is not { } adapter)
        {
            IntegrationsTelemetry.NotConfigured.Add(
                1, new KeyValuePair<string, object?>("capability", "calendar"));

            return Result.Failure<(ProviderKey, IExternalCalendarProvider, IntegrationAccess)>(
                IntegrationErrors.ProviderNotConfigured(provider.Value));
        }

        var resolved = await access.ResolveAsync(
            caller.Value.OwnerKind, caller.Value.OwnerId, provider, cancellationToken);

        return resolved.IsFailure
            ? Result.Failure<(ProviderKey, IExternalCalendarProvider, IntegrationAccess)>(resolved.Error)
            : Result.Success((provider, adapter, resolved.Value));
    }
}
