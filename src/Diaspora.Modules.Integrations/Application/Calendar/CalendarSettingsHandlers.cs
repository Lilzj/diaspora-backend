using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Integrations.Application.Mapping;
using Diaspora.Modules.Integrations.Application.Services;
using Diaspora.Modules.Integrations.Contracts.V1.Responses;
using Diaspora.Modules.Integrations.Domain.Calendar;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Integrations.Application.Calendar;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE16-GAP-006 — calendar settings persistence.
//
// The register's instruction is "copy B13's preference shape rather than inventing one", and the
// shape copied is the one that matters: a member-scoped row, defaults created on first read, and a
// partial write. The reason it is server-side at all is §42's account switching — today the settings
// are per-device under a member-scoped key, which works until somebody signs in on a second phone.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>This member's calendar preferences, defaulted if they have never set any.</summary>
[Authorize(Policy = IntegrationsPolicies.ConnectionsRead)]
public sealed record GetCalendarSettingsQuery : IQuery<CalendarSyncSettingsResponse>;

public sealed class GetCalendarSettingsQueryHandler
    : IQueryHandler<GetCalendarSettingsQuery, CalendarSyncSettingsResponse>
{
    private readonly IIntegrationCalendarSettingsRepository _settings;
    private readonly ICurrentUser _currentUser;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public GetCalendarSettingsQueryHandler(
        IIntegrationCalendarSettingsRepository settings,
        ICurrentUser currentUser,
        IGuidGenerator guids,
        IClock clock)
    {
        _settings = settings;
        _currentUser = currentUser;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<CalendarSyncSettingsResponse>> Handle(
        GetCalendarSettingsQuery query, CancellationToken cancellationToken)
    {
        var caller = IntegrationCaller.From(_currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<CalendarSyncSettingsResponse>(caller.Error);
        }

        var settings = await _settings.FindAsync(caller.Value.OwnerId, cancellationToken);

        // Composed, not persisted. A read must not write — a GET that inserts a row turns every
        // settings screen render into a database write and makes the endpoint non-idempotent for no
        // benefit. The row is created by the first PUT.
        return Result.Success(IntegrationMapper.ToSettings(
            settings
            ?? IntegrationCalendarSettings.CreateDefault(
                IntegrationSettingsId.From(_guids.NewSequentialGuid()),
                caller.Value.OwnerId,
                _clock.UtcNow)));
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Applies a partial settings update.
/// <para>
/// Null means "leave it alone" on every field. The client's contract is
/// <c>Partial&lt;CalendarSyncSettings&gt;</c>, and treating a missing field as an instruction to
/// clear would mean a client that sends only <c>enabled</c> wipes the member's default calendar.
/// </para>
/// </summary>
[Authorize(Policy = IntegrationsPolicies.SettingsWrite)]
public sealed record UpdateCalendarSettingsCommand : ICommand<CalendarSyncSettingsResponse>
{
    public bool? Enabled { get; init; }

    public string? DefaultCalendarId { get; init; }

    public string? DefaultProvider { get; init; }

    public bool? AutoExportRsvps { get; init; }

    public bool? TwoWaySyncEnabled { get; init; }

    public string? SyncFrequency { get; init; }

    public IReadOnlyList<int>? DefaultReminderMinutes { get; init; }

    public string? TimeZoneBehaviour { get; init; }

    public string? ConflictBehaviour { get; init; }
}

public sealed class UpdateCalendarSettingsCommandHandler
    : ICommandHandler<UpdateCalendarSettingsCommand, CalendarSyncSettingsResponse>
{
    private readonly IIntegrationCalendarSettingsRepository _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public UpdateCalendarSettingsCommandHandler(
        IIntegrationCalendarSettingsRepository settings,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IGuidGenerator guids,
        IClock clock)
    {
        _settings = settings;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<CalendarSyncSettingsResponse>> Handle(
        UpdateCalendarSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = IntegrationCaller.From(_currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<CalendarSyncSettingsResponse>(caller.Error);
        }

        var now = _clock.UtcNow;
        var settings = await _settings.FindAsync(caller.Value.OwnerId, cancellationToken);

        if (settings is null)
        {
            settings = IntegrationCalendarSettings.CreateDefault(
                IntegrationSettingsId.From(_guids.NewSequentialGuid()), caller.Value.OwnerId, now);

            _settings.Add(settings);
        }
        else
        {
            _settings.Update(settings);
        }

        settings.Apply(
            command.Enabled,
            command.DefaultCalendarId,
            command.DefaultProvider,
            command.AutoExportRsvps,
            command.TwoWaySyncEnabled,
            command.SyncFrequency,
            command.DefaultReminderMinutes,
            command.TimeZoneBehaviour,
            command.ConflictBehaviour,
            now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(IntegrationMapper.ToSettings(settings));
    }
}
