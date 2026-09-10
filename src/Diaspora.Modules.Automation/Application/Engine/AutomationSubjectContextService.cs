using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Automation.Application.Engine;

/// <summary>
/// What a trigger would carry about one named subject — Phase 5.
/// <para>
/// <b>The one thing a dry run was missing.</b> <c>POST /automations/simulate</c> took a
/// <c>SampleSubjectId</c> and built a trigger context whose <c>Values</c> dictionary was empty, so
/// every payload-carried condition came back with the same sentence — <i>"This trigger does not
/// carry the information the 'country' check needs"</i> — for triggers that carry it perfectly well.
/// This is where those values come from now.
/// </para>
/// <para>
/// <b>Two sources, in this order, and the order is the argument.</b> An armed horizon is not an
/// approximation of what will fire: for a horizon trigger it <i>is</i> what will fire, written by the
/// consumer that armed it, sitting in this module's own tables. Nothing beats reading the actual
/// payload. Only when there is no horizon does the owning module get asked, through
/// <see cref="IAutomationSubjectSource"/>, and only then does a cross-module question arise at all.
/// </para>
/// <para>
/// <b>No answer is an answer.</b> A subject nothing can describe returns <c>null</c>, and every
/// caller renders that as "this could not be previewed" rather than as a verdict. Inventing values
/// would show a member a match their real automation would not produce, which is the failure this
/// class exists to end rather than to relocate.
/// </para>
/// </summary>
public sealed class AutomationSubjectContextService
{
    private readonly IReadOnlyList<IAutomationSubjectSource> _sources;
    private readonly IAutomationSubjectHorizonRepository _horizons;
    private readonly ILogger<AutomationSubjectContextService> _logger;

    public AutomationSubjectContextService(
        IEnumerable<IAutomationSubjectSource> sources,
        IAutomationSubjectHorizonRepository horizons,
        ILogger<AutomationSubjectContextService> logger)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _sources = [.. sources];
        _horizons = horizons;
        _logger = logger;
    }

    /// <summary>
    /// The subject kind a trigger is about, or <c>null</c> where it is about no one thing.
    /// <para>
    /// Derived from the catalogue's own domain rather than declared a second time, so a trigger added
    /// to a domain inherits the mapping instead of needing one. The domains left out are left out on
    /// purpose: <c>community</c> spans announcements, posts and memberships and <c>monetization</c>
    /// spans subscriptions and placements, so neither has <i>a</i> subject kind; <c>search</c>,
    /// <c>location</c>, <c>analytics</c> and <c>notifications</c> have no subject at all.
    /// </para>
    /// </summary>
    public static string? SubjectKindFor(string triggerKey)
    {
        var trigger = AutomationCatalog.FindTrigger(triggerKey);

        return trigger?.Domain switch
        {
            "events" => AutomationSubjectKinds.Event,
            "jobs" => AutomationSubjectKinds.Job,
            "marketplace" => AutomationSubjectKinds.Listing,
            "housing" => AutomationSubjectKinds.Housing,
            "guides" => AutomationSubjectKinds.Guide,
            "providers" => AutomationSubjectKinds.Provider,
            "organizations" => AutomationSubjectKinds.Organization,
            "messaging" => AutomationSubjectKinds.Conversation,
            "creator" => AutomationSubjectKinds.Creator,
            "wallet" => AutomationSubjectKinds.Payment,
            _ => null
        };
    }

    /// <summary>
    /// The subject's state as this trigger would carry it, or <c>null</c>.
    /// </summary>
    public async Task<AutomationSubjectSnapshot?> ReadAsync(
        string triggerKey,
        Guid subjectId,
        UserId member,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(triggerKey) || subjectId == Guid.Empty)
        {
            return null;
        }

        var kind = SubjectKindFor(triggerKey);

        if (kind is null)
        {
            return null;
        }

        // ── The armed payload, where there is one ───────────────────────────────────────────────
        //
        // Matched on the trigger key as well as the subject, deliberately. A horizon armed for
        // `event.startingSoon` holds the values *that* trigger will carry; borrowing them to preview
        // an `event.created` rule would answer with a neighbouring trigger's dictionary and call it
        // this one's. The key sets genuinely differ — one carries `city`, the other `dayOfWeek` and
        // `timeOfDay` — so a member checking the day of the week would be told the trigger does not
        // carry it while a horizon sat right there. Better to fall through and ask the owner.
        var horizon = await _horizons.FindAsync(triggerKey, subjectId, cancellationToken);

        if (horizon is not null)
        {
            return new AutomationSubjectSnapshot
            {
                SubjectKind = string.IsNullOrWhiteSpace(horizon.SubjectKind)
                    ? kind
                    : horizon.SubjectKind,
                SubjectId = subjectId,
                Title = horizon.SubjectTitle,
                Moment = horizon.SubjectMoment,
                TimeZoneId = horizon.TimeZoneId,
                Values = Services.AutomationSubjectHorizonService.ReadValues(horizon.Values)
            };
        }

        // ── The module that owns it ─────────────────────────────────────────────────────────────
        var source = _sources.FirstOrDefault(candidate =>
            candidate.Answers.Contains(kind, StringComparer.Ordinal));

        if (source is null)
        {
            return null;
        }

        try
        {
            return await source.ReadAsync(
                new AutomationSubjectQuery(kind, subjectId, member), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Fails closed, exactly as a condition source does. A module having a bad minute must
            // produce "this could not be previewed" rather than a 500 on a builder screen.
            _logger.LogWarning(
                exception,
                "A subject source for {SubjectKind} could not answer; the preview will say so.",
                kind);

            return null;
        }
    }
}
