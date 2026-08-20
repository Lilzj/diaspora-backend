using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Automation.Application.Services;

/// <summary>
/// Keeps the durable due-store in step with what the definitions say.
/// <para>
/// <b>One occurrence ahead, never a horizon.</b> A schedule that materialised a year of occurrences
/// would have to be un-materialised on every edit, every pause and every timezone change, and each of
/// those is a place to leave a stale row that fires anyway. Keeping exactly the next one means an edit
/// cancels one row and writes one row, and a pause cancels one row.
/// </para>
/// <para>
/// <b>Every write is idempotent against the unique index.</b> Two instances re-deriving the same next
/// occurrence produce one row, and the second insert is refused by the database rather than by a read
/// that both would pass.
/// </para>
/// </summary>
public sealed class AutomationScheduleService
{
    private readonly IAutomationOccurrenceRepository _occurrences;
    private readonly IClock _clock;
    private readonly ILogger<AutomationScheduleService> _logger;

    public AutomationScheduleService(
        IAutomationOccurrenceRepository occurrences,
        IClock clock,
        ILogger<AutomationScheduleService> logger)
    {
        _occurrences = occurrences;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Makes the store agree with the definition: cancels what is pending, schedules the next.
    /// <para>
    /// Called on create, on edit, on resume and after every scheduled run. Returns how many pending
    /// occurrences it cancelled, which is the count §40 requires the UI to report rather than guess.
    /// </para>
    /// </summary>
    public async Task<int> SynchroniseAsync(
        AutomationDefinition automation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(automation);

        var now = _clock.UtcNow;
        var cancelled = await CancelPendingAsync(automation, "The automation changed.", cancellationToken);

        if (automation.Status != AutomationStatus.Active || !automation.HasSchedule)
        {
            automation.SetNextRunAt(null);
            await _occurrences.SaveChangesAsync(cancellationToken);
            return cancelled;
        }

        var plan = AutomationScheduleCalculator.Next(automation, now);

        if (plan.NextRunAt is not { } dueAt)
        {
            // A schedule with no next moment is not a defect: a one-off that has passed, a custom
            // description the platform does not parse, an end date reached. The automation says so
            // rather than claiming a time it does not have.
            automation.SetNextRunAt(null);
            await _occurrences.SaveChangesAsync(cancellationToken);
            return cancelled;
        }

        var existing = await _occurrences.FindAsync(automation.Id, dueAt, cancellationToken);

        if (existing is null)
        {
            _occurrences.Add(AutomationOccurrence.Schedule(
                automation.Id, automation.OwnerId, dueAt, plan.TimeZoneId, plan.LocalWallClock, now));
        }

        automation.SetNextRunAt(dueAt);
        await _occurrences.SaveChangesAsync(cancellationToken);

        return cancelled;
    }

    /// <summary>
    /// Cancels every pending occurrence, and says how many.
    /// <para>
    /// A pause must cancel pending occurrences rather than merely stop new ones being created, and a
    /// delete must cancel them irrevocably. The count is returned so the UI can say what happened
    /// rather than implying it.
    /// </para>
    /// </summary>
    public async Task<int> CancelPendingAsync(
        AutomationDefinition automation, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(automation);

        var pending = await _occurrences.ListPendingForAutomationAsync(automation.Id, cancellationToken);
        var now = _clock.UtcNow;

        foreach (var occurrence in pending)
        {
            occurrence.Cancel(reason, now);
        }

        if (pending.Count > 0)
        {
            _logger.LogDebug(
                "Cancelled {Count} pending occurrences for automation {AutomationId}.",
                pending.Count, automation.Id.Value);
        }

        return pending.Count;
    }

    /// <summary>The next occurrence and its warnings, for the schedule endpoint. Writes nothing.</summary>
    public AutomationOccurrencePlan Preview(AutomationDefinition automation) =>
        AutomationScheduleCalculator.Next(automation, _clock.UtcNow);
}
