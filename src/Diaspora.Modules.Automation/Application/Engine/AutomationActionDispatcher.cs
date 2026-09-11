using System.Diagnostics;
using System.Text.Json;
using Diaspora.Contracts.Automation.V1.Events;
using Diaspora.Modules.Automation.Catalog;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.Runs;
using Diaspora.Modules.Automation.Domain.Scheduling;
using Diaspora.Modules.Automation.Extensibility.Actions;
using Diaspora.Modules.Automation.Extensibility.Scopes;
using Diaspora.Modules.Automation.Observability;
using Diaspora.Modules.Automation.Options;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Automation.Application.Engine;

/// <summary>
/// Carries out the allow-listed things an automation may ask for.
/// <para>
/// <b>Every branch below is a <c>case</c> over a closed set of catalogue keys.</b> There is no
/// reflection, no handler resolved from a stored string, no URL, no script and no HTTP client. An
/// action a member configured can only be one of twelve, and each of the twelve is a method here.
/// </para>
/// <para>
/// <b>The dry run is enforced here rather than by the caller.</b> A flag the dispatcher trusted is a
/// flag somebody ignores once, and the consequence is a member's community receiving a test
/// broadcast. Every method returns before its side effect when <c>isSimulated</c> is set, and the
/// simulation path and the real path share the same authorization and the same recipient derivation
/// so a dry run is a truthful preview rather than a different code path.
/// </para>
/// <para>
/// <b>Nothing here moves money.</b> There is no payment call, no ledger write and no wallet read —
/// there is no action key that could reach one, and an architecture test asserts this assembly names
/// no financial type.
/// </para>
/// </summary>
public sealed class AutomationActionDispatcher
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IAutomationReminderRepository _reminders;
    private readonly AutomationScopeAuthorityResolver _scopes;
    private readonly IAutomationCalendarWriter _calendar;
    private readonly IOptions<AutomationOptions> _options;
    private readonly ILogger<AutomationActionDispatcher> _logger;

    public AutomationActionDispatcher(
        IIntegrationEventPublisher publisher,
        IAutomationReminderRepository reminders,
        AutomationScopeAuthorityResolver scopes,
        IAutomationCalendarWriter calendar,
        IOptions<AutomationOptions> options,
        ILogger<AutomationActionDispatcher> logger)
    {
        _publisher = publisher;
        _reminders = reminders;
        _scopes = scopes;
        _calendar = calendar;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Performs one action and records what it produced on the run.
    /// </summary>
    public async Task DispatchAsync(
        AutomationDefinition automation,
        AutomationActionStep action,
        AutomationRun run,
        AutomationTriggerContext context,
        AutomationScopeGrant grant,
        bool isSimulated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(automation);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(context);

        // B24-G37. Measured around the whole dispatch, refusals included: an action refused because
        // its owner lost a capability is fast, and a run that spends its time being refused is a
        // different diagnosis from one that spends its time waiting on a provider. The timer stops in
        // a finally so a thrown action is still timed — an action that fails slowly is the case an
        // operator most wants to see.
        var started = Stopwatch.GetTimestamp();

        try
        {
            await DispatchCoreAsync(
                automation, action, run, context, grant, isSimulated, now, cancellationToken);
        }
        finally
        {
            AutomationTelemetry.ActionTook(
                action.Key, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private async Task DispatchCoreAsync(
        AutomationDefinition automation,
        AutomationActionStep action,
        AutomationRun run,
        AutomationTriggerContext context,
        AutomationScopeGrant grant,
        bool isSimulated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var definition = AutomationCatalog.FindAction(action.Key);

        if (definition is null)
        {
            // A stored action key the catalogue no longer has. The rule is untrusted input and must
            // fail closed: an action that no longer exists is not an action that quietly does nothing.
            Record(run, action, action.Key, AutomationActionOutcomeStatus.Failed,
                null, AutomationFailureKind.InvalidConfiguration,
                "This action is no longer one the platform can perform.", now);
            return;
        }

        // ── The capability re-check, and the reason it is here rather than at configuration time ──
        //
        // A moderator who created a community automation and then lost the role must stop being able
        // to broadcast through it. Configuration time cannot know that; the moment before dispatch is
        // the only place it can be enforced, so it is enforced here even though the write path already
        // checked it.
        if (definition.RequiredCapability is { } capability &&
            !grant.Capabilities.Contains(capability, StringComparer.Ordinal))
        {
            AutomationTelemetry.ActionDenied(action.Key, capability);

            Record(run, action, definition.Key, AutomationActionOutcomeStatus.Failed,
                null, AutomationFailureKind.PermissionDenied,
                "Whoever set this up no longer has permission to do it.", now);
            return;
        }

        if (definition.ReachesOtherPeople && !grant.CanReachMembers)
        {
            // The authority's own reason where it gave one. "No longer has permission" is the right
            // sentence for an administrator who lost their role, and the wrong one for the owner of
            // a community that has not been opened yet — they hold the permission, and there is
            // simply nobody to address.
            Record(run, action, definition.Key, AutomationActionOutcomeStatus.Failed,
                null, AutomationFailureKind.PermissionDenied,
                grant.ReachRefusal
                ?? "Whoever set this up no longer has permission to notify these members.", now);
            return;
        }

        switch (definition.Key)
        {
            case "notify.self":
                await NotifyOwnerAsync(
                    automation, action, run, context, AutomationNotificationKeys.OwnerAudience,
                    isSimulated, now, cancellationToken);
                break;

            case "notify.push":
                await NotifyOwnerAsync(
                    automation, action, run, context, AutomationNotificationKeys.OwnerPushAudience,
                    isSimulated, now, cancellationToken);
                break;

            case "notify.email":
                await NotifyOwnerAsync(
                    automation, action, run, context, AutomationNotificationKeys.OwnerEmailAudience,
                    isSimulated, now, cancellationToken);
                break;

            case "notify.community":
                await BroadcastAsync(
                    automation, action, run, context, grant,
                    AutomationNotificationKeys.CommunityAudience, isSimulated, now, cancellationToken);
                break;

            case "notify.organization":
                await BroadcastAsync(
                    automation, action, run, context, grant,
                    AutomationNotificationKeys.OrganizationAudience, isSimulated, now, cancellationToken);
                break;

            case "notify.followers":
                await BroadcastAsync(
                    automation, action, run, context, grant,
                    AutomationNotificationKeys.FollowerAudience, isSimulated, now, cancellationToken);
                break;

            case "reminder.schedule":
                await ScheduleReminderAsync(
                    automation, action, run, context, isSimulated, now, cancellationToken);
                break;

            case "calendar.addEvent":
            case "calendar.updateEvent":
            case "calendar.removeEvent":
                await WriteCalendarAsync(
                    automation, action, run, context, definition.Key, isSimulated, now, cancellationToken);
                break;

            case "item.save":
            case "open.deepLink":
                // Genuinely the device's work, and the server does not claim otherwise. §57 forbids an
                // unperformed outcome looking like a performed one, so this is skipped with a reason
                // rather than reported as succeeded.
                Record(run, action, definition.Key, AutomationActionOutcomeStatus.Skipped,
                    "This happens on your device when you open the notification.",
                    AutomationFailureKind.None, null, now);
                break;

            default:
                Record(run, action, definition.Key, AutomationActionOutcomeStatus.Failed,
                    null, AutomationFailureKind.InvalidConfiguration,
                    "This action is not one the platform can perform.", now);
                break;
        }
    }

    // ── Notifications ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asks the Communication Platform to tell the automation's own owner.
    /// <para>
    /// Publishes a fact and stops. This module names no channel, no template, no priority and no
    /// category — B13's routing rules supply every one of them — which is what keeps "an automation
    /// decides when, B13 decides how" a structural property rather than a convention, and what makes
    /// a member's preferences, quiet hours and digest window apply without this module knowing they
    /// exist.
    /// </para>
    /// </summary>
    private async Task NotifyOwnerAsync(
        AutomationDefinition automation,
        AutomationActionStep action,
        AutomationRun run,
        AutomationTriggerContext context,
        string audience,
        bool isSimulated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var recipient = ResolveOwnerRecipient(automation);

        if (recipient is null)
        {
            Record(run, action, action.Key, AutomationActionOutcomeStatus.Failed,
                null, AutomationFailureKind.InvalidConfiguration,
                "This automation has no member to notify.", now);
            return;
        }

        if (isSimulated)
        {
            Record(run, action, action.Key, AutomationActionOutcomeStatus.Skipped,
                "Nothing was sent. This was a test.", AutomationFailureKind.None, null, now);
            return;
        }

        await PublishNotificationAsync(
            automation, action, run, context, audience, recipient, null, cancellationToken);

        // Pending, not succeeded. The Communication Platform has not answered yet, and a run that
        // claimed delivery before the answer arrived would be the frontend's §77 violation committed
        // on the server. The settlement receipt closes it.
        Record(run, action, action.Key, AutomationActionOutcomeStatus.Pending,
            null, AutomationFailureKind.None, "Sent to the notification platform.", null);
    }

    /// <summary>
    /// Asks the Communication Platform to tell a scope's members.
    /// <para>
    /// <b>The recipient set is derived here, from the scope, and never read from the rule.</b> §49
    /// requires it and the domain model enforces it — there is no recipient field on an action — but
    /// the derivation is also where the ceiling lives: a broadcast beyond
    /// <see cref="AutomationOptions.MaximumBroadcastRecipients"/> is refused with a recorded reason
    /// rather than truncated silently, because a truncated broadcast reads to a moderator as a
    /// delivered one.
    /// </para>
    /// </summary>
    private async Task BroadcastAsync(
        AutomationDefinition automation,
        AutomationActionStep action,
        AutomationRun run,
        AutomationTriggerContext context,
        AutomationScopeGrant grant,
        string audience,
        bool isSimulated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var authority = _scopes.For(automation.OwnerKind);
        var estimate = await authority.EstimateAudienceAsync(
            automation.OwnerKind, automation.OwnerId, cancellationToken);

        var ceiling = _options.Value.MaximumBroadcastRecipients;

        if (estimate.EstimatedRecipients is { } count && count > ceiling)
        {
            Record(run, action, action.Key, AutomationActionOutcomeStatus.Failed,
                null, AutomationFailureKind.RateLimited,
                $"This would reach more than {ceiling} people, which is more than an automation may "
                + "notify at once.", now);
            return;
        }

        if (isSimulated)
        {
            Record(run, action, action.Key, AutomationActionOutcomeStatus.Skipped,
                estimate.EstimatedRecipients is { } estimated
                    ? $"Nothing was sent. This was a test; it would have reached about {estimated} people."
                    : "Nothing was sent. This was a test.",
                AutomationFailureKind.None, null, now);
            return;
        }

        var page = 0;
        var published = 0;

        while (published < ceiling)
        {
            var recipients = await authority.ListRecipientsAsync(
                automation.OwnerKind,
                automation.OwnerId,
                page * _options.Value.BroadcastPageSize,
                _options.Value.BroadcastPageSize,
                cancellationToken);

            if (recipients.Count == 0)
            {
                break;
            }

            foreach (var recipient in recipients)
            {
                if (published >= ceiling)
                {
                    break;
                }

                await PublishNotificationAsync(
                    automation, action, run, context, audience, recipient, grant.Label,
                    cancellationToken);

                published++;
            }

            page++;
        }

        if (published == 0)
        {
            // Nobody to tell, or no module able to enumerate this scope. Skipped with a reason rather
            // than reported as a delivered broadcast — the register's rule that a member must not be
            // shown a success the platform did not produce.
            Record(run, action, action.Key, AutomationActionOutcomeStatus.Skipped,
                "This deployment could not work out who to notify for this scope, so nothing was sent.",
                AutomationFailureKind.None, null, now);
            return;
        }

        AutomationTelemetry.BroadcastPublished(action.Key, published);

        Record(run, action, action.Key, AutomationActionOutcomeStatus.Pending,
            null, AutomationFailureKind.None,
            $"Sent to the notification platform for {published} members.", null);
    }

    private Task PublishNotificationAsync(
        AutomationDefinition automation,
        AutomationActionStep action,
        AutomationRun run,
        AutomationTriggerContext context,
        string audience,
        UserId recipient,
        string? scopeName,
        CancellationToken cancellationToken) =>
        _publisher.PublishAsync(
            new AutomationDeliveryRequestedIntegrationEvent
            {
                AutomationId = automation.Id.Value,
                RunId = run.Id.Value,
                ActionId = action.Id.Value,
                RecipientUserId = recipient.Value,
                Audience = audience,
                AutomationName = automation.Name,

                // The trigger's own summary, which the consumer built from public subject fields.
                // Never a condition comparison: a message in an inbox outlives the data it quotes.
                Summary = context.Summary,
                ScopeName = scopeName ?? automation.OwnerLabel,
                SubjectType = context.SubjectType,
                SubjectId = context.SubjectId?.ToString("D"),
                SubjectTitle = context.SubjectTitle,
                SubjectMoment = context.SubjectMoment,
                OccurredAt = context.OccurredAt
            },
            cancellationToken);

    // ── Reminders ───────────────────────────────────────────────────────────────────────────────

    private async Task ScheduleReminderAsync(
        AutomationDefinition automation,
        AutomationActionStep action,
        AutomationRun run,
        AutomationTriggerContext context,
        bool isSimulated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var recipient = ResolveOwnerRecipient(automation);

        if (recipient is null ||
            context.SubjectId is not { } subjectId ||
            context.SubjectMoment is not { } moment)
        {
            Record(run, action, action.Key, AutomationActionOutcomeStatus.Skipped,
                "This trigger has no date to count back from, so no reminder was set.",
                AutomationFailureKind.None, null, now);
            return;
        }

        var parameters = ReadParameters(action.Parameters);
        var offset = ReadInt(parameters, "offsetMinutes") ?? 60;
        var anchor = ReadString(parameters, "anchor") ?? "start";

        // The subject's own zone, never the device's. An event in Lagos does not move because the
        // member flew to London, and a reminder computed in the device's zone would arrive an hour
        // out for half the year in every zone that observes daylight saving.
        var zone = context.SubjectTimeZoneId ?? "UTC";
        var dueAt = moment.AddMinutes(-offset);

        if (dueAt <= now)
        {
            // Past the moment it was about. A late reminder is worse than none, so it is skipped with
            // a record rather than delivered immediately.
            Record(run, action, action.Key, AutomationActionOutcomeStatus.Skipped,
                "That reminder time has already passed, so nothing was scheduled.",
                AutomationFailureKind.None, null, now);
            return;
        }

        if (isSimulated)
        {
            Record(run, action, action.Key, AutomationActionOutcomeStatus.Skipped,
                "Nothing was scheduled. This was a test.", AutomationFailureKind.None, null, now);
            return;
        }

        // The subject's own kind, never the literal "event".
        //
        // While every reminder was about a community event, hard-coding it was invisible. It stops
        // being invisible the moment a job closing, a listing expiry, a reservation ending or a
        // viewing appointment can also be reminded about: the deduplication key is
        // (member, subjectKind, subjectId, offset, anchor), so two subjects of different kinds that
        // happened to share an identifier would collide and the second member's reminder would be
        // suppressed as a duplicate of the first's — and every reminder would deep-link into
        // Community Life whatever it was about.
        var subjectKind = AutomationSubjectKinds.Normalise(context.SubjectType);

        var key = AutomationReminder.BuildKey(recipient, subjectKind, subjectId, offset, anchor);
        var existing = await _reminders.FindByKeyAsync(key, cancellationToken);

        if (existing is not null && existing.IsPending)
        {
            // The mandatory idempotency on (member, subject, offset, anchor). A second identical
            // reminder is two identical pushes a minute apart, which is the most common way an
            // automation feature loses a member's trust.
            Record(run, action, action.Key, AutomationActionOutcomeStatus.Suppressed,
                "You already have this reminder set.", AutomationFailureKind.None, null, now);
            return;
        }

        var reminder = AutomationReminder.Configure(
            recipient, subjectKind, subjectId,
            context.SubjectTitle ?? AutomationSubjectKinds.FallbackTitle(subjectKind),
            moment, offset, anchor, zone, now);

        reminder.MarkScheduled(dueAt);
        _reminders.Add(reminder);

        Record(run, action, action.Key, AutomationActionOutcomeStatus.Succeeded,
            null, AutomationFailureKind.None,
            $"Reminder set for {AutomationScheduleCalculator.Describe(dueAt, zone)}.", now,
            "reminder", reminder.Id.Value.ToString("D"),
            AutomationSubjectKinds.DeepLink(subjectKind, subjectId));
    }

    // ── Calendar ────────────────────────────────────────────────────────────────────────────────

    private async Task WriteCalendarAsync(
        AutomationDefinition automation,
        AutomationActionStep action,
        AutomationRun run,
        AutomationTriggerContext context,
        string key,
        bool isSimulated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var recipient = ResolveOwnerRecipient(automation);

        if (recipient is null || context.SubjectId is not { } subjectId)
        {
            Record(run, action, key, AutomationActionOutcomeStatus.Skipped,
                "This trigger has nothing that could be put in a calendar.",
                AutomationFailureKind.None, null, now);
            return;
        }

        if (!_calendar.IsConfigured)
        {
            // The external dependency, reported honestly. FRONTEND-PHASE18-GAP-025 puts the proxied
            // write with the integration platform; with no adapter configured, the run says the
            // platform cannot do this rather than that it tried and failed.
            Record(run, action, key, AutomationActionOutcomeStatus.Skipped,
                "This deployment cannot write to a connected calendar on your behalf. Add it from "
                + "the event screen while the app is open instead.",
                AutomationFailureKind.None, null, now);
            return;
        }

        if (isSimulated)
        {
            Record(run, action, key, AutomationActionOutcomeStatus.Skipped,
                "Nothing was written. This was a test.", AutomationFailureKind.None, null, now);
            return;
        }

        var moment = context.SubjectMoment ?? context.OccurredAt;
        var zone = context.SubjectTimeZoneId ?? "UTC";
        var title = context.SubjectTitle ?? automation.Name;

        var result = key switch
        {
            "calendar.addEvent" => await _calendar.AddAsync(
                recipient, context.SubjectType ?? "event", subjectId, title, moment, null, zone,
                cancellationToken),
            "calendar.updateEvent" => await _calendar.UpdateAsync(
                recipient, context.SubjectType ?? "event", subjectId, title, moment, null, zone,
                cancellationToken),
            _ => await _calendar.RemoveAsync(
                recipient, context.SubjectType ?? "event", subjectId, cancellationToken)
        };

        if (result.Succeeded)
        {
            Record(run, action, key, AutomationActionOutcomeStatus.Succeeded,
                null, AutomationFailureKind.None, null, now,
                "calendar", result.ExternalId, null);
            return;
        }

        Record(run, action, key,
            result.NotConfigured
                ? AutomationActionOutcomeStatus.Skipped
                : AutomationActionOutcomeStatus.Failed,
            result.NotConfigured ? result.Message : null,
            result.NotConfigured ? AutomationFailureKind.None : AutomationFailureKind.ProviderUnavailable,
            result.NotConfigured ? null : result.Message,
            now);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Who "me" is on an automation.
    /// <para>
    /// The owner where the owner is a person, and the member who created it where the owner is a
    /// group — a community automation's <c>notify.self</c> means the moderator who wrote it. Nothing
    /// here reads a recipient from the rule, because the rule has no field for one.
    /// </para>
    /// </summary>
    private static UserId? ResolveOwnerRecipient(AutomationDefinition automation) =>
        automation.OwnerKind switch
        {
            AutomationOwnerKind.Community or AutomationOwnerKind.Organization
                or AutomationOwnerKind.Platform =>
                automation.CreatedByUserId == UserId.Empty ? null : automation.CreatedByUserId,
            _ => automation.OwnerId == Guid.Empty ? null : UserId.From(automation.OwnerId)
        };

    private static void Record(
        AutomationRun run,
        AutomationActionStep action,
        string key,
        AutomationActionOutcomeStatus status,
        string? suppressionReason,
        AutomationFailureKind failureKind,
        string? message,
        DateTimeOffset? completedAt,
        string? resultKind = null,
        string? resultId = null,
        string? deepLink = null)
    {
        run.RecordAction(
            action.Id, key, key, status, suppressionReason, failureKind, message,
            resultKind, resultId, deepLink, completedAt);

        AutomationTelemetry.ActionCompleted(key, status.ToString());
    }

    /// <summary>
    /// Reads an action's parameter bag.
    /// <para>
    /// Scalars only, and a malformed bag reads as empty rather than throwing: the bag was written by a
    /// client and a rule stored months ago must not be able to crash the worker that runs it.
    /// </para>
    /// </summary>
    private static Dictionary<string, string?> ReadParameters(string json)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

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
                values[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Array => string.Join(
                        ',', property.Value.EnumerateArray().Select(element => element.ToString())),
                    _ => null
                };
            }
        }
        catch (JsonException)
        {
            return values;
        }

        return values;
    }

    private static int? ReadInt(Dictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed) ? parsed : null;

    private static string? ReadString(Dictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : null;
}
