using System.Globalization;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Automation.Application.Engine;

/// <summary>
/// What a consumed contract, a due occurrence or a manual press yields: facts, and no decisions.
/// <para>
/// <b>This record is where "an automation reacts to a fact and never owns it" is enforced.</b> A
/// consumer extracts identifiers and values from a published contract and builds one of these. It
/// cannot name a condition, an action, a recipient or a template — none of those fields exist here.
/// The rule the member wrote supplies every one of them.
/// </para>
/// <para>
/// <b><see cref="Values"/> is the entire surface a condition can be answered from.</b> An evaluator
/// has this dictionary and nothing else: no repository, no HTTP client, no query into another
/// module's tables. A condition whose answer is not in the payload is unevaluable and says so, which
/// is what keeps a condition engine from becoming a cross-module read.
/// </para>
/// </summary>
public sealed record AutomationTriggerContext
{
    /// <summary>The catalogue key this fires. One contract may produce several.</summary>
    public required string TriggerKey { get; init; }

    /// <summary>
    /// The published event's identity, or the occurrence's, or the manual attempt's key.
    /// <para>
    /// The idempotency subject with the automation. A redelivered message carries the same one, which
    /// is what makes the second delivery find the first run instead of starting another.
    /// </para>
    /// </summary>
    public required string TriggerEventId { get; init; }

    /// <summary>
    /// Who this concerns.
    /// <para>
    /// The engine matches automations whose owner is one of these. Several because one fact often
    /// concerns several people differently — an application submitted concerns the candidate and the
    /// employer — and a consumer that could only name one would silently drop the other's automations.
    /// </para>
    /// </summary>
    public required IReadOnlyList<Guid> ConcernedOwnerIds { get; init; }

    /// <summary>What the fact is about — <c>event</c>, <c>listing</c>, <c>job</c>, <c>guide</c>.</summary>
    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    /// <summary>The subject's own title, for the summary and for a reminder's wording.</summary>
    public string? SubjectTitle { get; init; }

    /// <summary>
    /// The subject's own moment and zone, where it has one.
    /// <para>
    /// Carried so an event-relative offset is computed where the event <i>is</i>. An event in Lagos
    /// does not move because the member flew to London, and a schedule that used the device's zone
    /// would move it by an hour twice a year regardless.
    /// </para>
    /// </summary>
    public DateTimeOffset? SubjectMoment { get; init; }

    public string? SubjectTimeZoneId { get; init; }

    /// <summary>What set it off, in the member's words. Public facts only.</summary>
    public required string Summary { get; init; }

    /// <summary>When the fact happened, from the published contract. Not when it was delivered.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// The values a condition may be answered from.
    /// <para>
    /// Keys are condition keys where the payload answers one directly — <c>country</c>,
    /// <c>applicationStatus</c>, <c>amountAbove</c> — so an evaluator is a lookup rather than a
    /// per-trigger switch. Comparison is ordinal and case-insensitive on keys.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string?> Values { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The answers the owning modules gave, for the conditions no payload may answer — Wave 12.
    /// <para>
    /// <b>A separate dictionary, and the separation is the security property.</b> <c>walletBalance</c>,
    /// <c>subscriptionActive</c>, <c>creatorStatus</c>, <c>metricAbove</c> and <c>metricBelow</c> are
    /// read <i>only</i> from here. If they shared <see cref="Values"/>, a consumer over any published
    /// contract — including one written by a module with no business knowing a member's balance —
    /// could decide whether a balance condition matched by putting a number in a payload.
    /// </para>
    /// <para>
    /// Filled by <c>AutomationConditionResolver</c> immediately before evaluation, from the automation
    /// owner's own identity, and empty for every run whose rule asks nothing of the kind — which is
    /// almost all of them.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string?> ResolvedValues { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How deep in a chain of automations this trigger sits.
    /// <para>
    /// Zero for anything a person or a module caused. Incremented when an automation's own action
    /// produced the fact, which is what lets the engine refuse a runaway chain rather than discover it
    /// from a member's inbox.
    /// </para>
    /// </summary>
    public int ChainDepth { get; init; }

    /// <summary>The identity of the whole chain, so an operator can see all of it at once.</summary>
    public string ChainId { get; init; } = string.Empty;

    /// <summary>
    /// Whether this context is a dry run's rather than a real fact's — Phase 5.
    /// <para>
    /// <b>It changes one thing: what a member is told when a value is absent.</b> A real trigger that
    /// does not carry a condition's key genuinely does not carry it, and the honest answer names the
    /// trigger. A preview that could not describe its subject has a different reason for the same
    /// absence, and telling a member their trigger is at fault — for a trigger that carries the field
    /// perfectly well when it really fires — sends them to change a rule that was never wrong.
    /// </para>
    /// <para>
    /// Set by the engine from the run's own <c>isSimulated</c>, never by a caller, so the two cannot
    /// disagree about which kind of run this is.
    /// </para>
    /// </summary>
    public bool IsPreview { get; init; }

    /// <summary>Set only when a member pressed the button. The engine refuses any other manual claim.</summary>
    public UserId? ManualActor { get; init; }

    /// <summary>Builds the values dictionary with the comparer the evaluator expects.</summary>
    public static Dictionary<string, string?> NewValues() =>
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Formats a value the way the evaluator parses it. Invariant, always.</summary>
    public static string? Scalar(object? value) => value switch
    {
        null => null,
        string text => text,
        bool flag => flag ? "true" : "false",
        DateTimeOffset moment => moment.ToString("O", CultureInfo.InvariantCulture),
        DateTime moment => moment.ToString("O", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString(CultureInfo.InvariantCulture),
        Guid id => id.ToString("D"),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}
