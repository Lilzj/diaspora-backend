using System.Text.Json;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.Scheduling;
using Diaspora.Modules.Automation.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Automation.Application.Services;

/// <summary>
/// Arms, moves and cancels the dated subjects the four horizon triggers fire from — Wave 12.
/// <para>
/// <b>Every consumer that knows a date calls exactly one of three methods here.</b> Keeping the
/// arithmetic, the rebasing rule and the "did it already fire" question in one class is what stops
/// four consumers each getting the redelivery case slightly differently — which is the failure this
/// module has the least tolerance for, because the symptom is a member's second identical
/// notification.
/// </para>
/// <para>
/// <b>Nothing here decides who is told.</b> It writes a row describing a subject and a moment. Which
/// automations match, whether their conditions hold and what they then do is the engine's, at due
/// time, through exactly the path an event-driven trigger takes.
/// </para>
/// </summary>
public sealed class AutomationSubjectHorizonService
{
    private readonly IAutomationSubjectHorizonRepository _horizons;
    private readonly IClock _clock;
    private readonly IOptions<AutomationOptions> _options;
    private readonly ILogger<AutomationSubjectHorizonService> _logger;

    public AutomationSubjectHorizonService(
        IAutomationSubjectHorizonRepository horizons,
        IClock clock,
        IOptions<AutomationOptions> options,
        ILogger<AutomationSubjectHorizonService> logger)
    {
        _horizons = horizons;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Remembers a dated subject, or moves the one already remembered.
    /// <para>
    /// Returns false, without writing, in the two cases that are ordinary rather than exceptional: the
    /// moment is already past or so close that the lead cannot be applied, and the horizon already
    /// fired. Neither is logged as a problem — a listing republished the day it expires and a
    /// redelivered publication are both normal traffic.
    /// </para>
    /// </summary>
    public async Task<bool> ArmAsync(
        string triggerKey,
        string subjectKind,
        Guid subjectId,
        string subjectTitle,
        DateTimeOffset subjectMoment,
        string? timeZoneId,
        IReadOnlyList<Guid> concernedOwnerIds,
        IReadOnlyDictionary<string, string?> values,
        string summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (subjectId == Guid.Empty)
        {
            return false;
        }

        var trigger = AutomationCatalog.FindTrigger(triggerKey);

        if (trigger is null || !trigger.IsAvailable)
        {
            // A catalogue that shrank. Nothing is written, because a row nothing can fire is a row
            // that only ever becomes stale.
            return false;
        }

        var now = _clock.UtcNow;
        var dueAt = subjectMoment.AddHours(-_options.Value.SubjectHorizonLeadHours);

        var existing = await _horizons.FindAsync(triggerKey, subjectId, cancellationToken);

        if (dueAt <= now)
        {
            // Too late to give the member the lead the trigger promises. Firing now would tell somebody
            // a job closes "soon" when it closes in an hour, or has already closed — and the frontend's
            // own wording for all four of these is "a set time before".
            //
            // A horizon already written for an earlier date is cancelled rather than left pending, so a
            // listing whose expiry was brought forward stops being remembered.
            if (existing is not null)
            {
                existing.Cancel(
                    "The subject's date moved to within the platform's notice period.", now);

                await _horizons.SaveChangesAsync(cancellationToken);
            }

            return false;
        }

        var serialized = Serialize(values);

        if (existing is not null)
        {
            var rebased = existing.Rebase(subjectMoment, dueAt, subjectTitle, summary, serialized);

            if (rebased)
            {
                await _horizons.SaveChangesAsync(cancellationToken);
            }

            // False when it had already fired or been cancelled: a redelivered publication must not
            // re-arm a horizon the member has already heard about.
            return rebased;
        }

        _horizons.Add(AutomationSubjectHorizon.Schedule(
            triggerKey,
            AutomationSubjectKinds.Normalise(subjectKind),
            subjectId,
            subjectTitle,
            subjectMoment,
            dueAt,
            timeZoneId ?? "UTC",
            concernedOwnerIds,
            serialized,
            summary,
            now));

        await _horizons.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The subject went away. Cancels every pending horizon on it, whatever the trigger.
    /// <para>
    /// By subject rather than by trigger key, and that is the point: a listing that sold has both an
    /// expiry horizon and a reservation-ending horizon, and a caller that had to name each one would
    /// eventually forget one and remind somebody about a listing that is gone.
    /// </para>
    /// </summary>
    public async Task<int> CancelForSubjectAsync(
        Guid subjectId, string reason, CancellationToken cancellationToken = default)
    {
        if (subjectId == Guid.Empty)
        {
            return 0;
        }

        var pending = await _horizons.ListForSubjectAsync(subjectId, cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;

        foreach (var horizon in pending)
        {
            horizon.Cancel(reason, now);
        }

        await _horizons.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Cancelled {Count} subject horizons on subject {SubjectId}.", pending.Count, subjectId);

        return pending.Count;
    }

    /// <summary>
    /// Reads a horizon's stored condition values back.
    /// <para>
    /// A malformed bag reads as empty rather than throwing. The row may have been written by an older
    /// deployment, and a background sweep must not die on one.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<string, string?> ReadValues(string json)
    {
        var values = AutomationTriggerContextValues();

        if (string.IsNullOrWhiteSpace(json))
        {
            return values;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return values;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.ValueKind == JsonValueKind.Null
                    ? null
                    : property.Value.GetString();
            }
        }
        catch (JsonException)
        {
            return values;
        }

        return values;
    }

    /// <summary>
    /// Scalars only, and nulls dropped.
    /// <para>
    /// The bag holds condition values — a country code, a category key, a status — and every one is
    /// already a string by the time a consumer puts it there. Serializing anything richer would make
    /// this a place a payload could smuggle structure into the evaluator.
    /// </para>
    /// </summary>
    private static string Serialize(IReadOnlyDictionary<string, string?> values)
    {
        var populated = values
            .Where(entry => entry.Value is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        return populated.Count == 0 ? "{}" : JsonSerializer.Serialize(populated);
    }

    private static Dictionary<string, string?> AutomationTriggerContextValues() =>
        new(StringComparer.OrdinalIgnoreCase);
}
