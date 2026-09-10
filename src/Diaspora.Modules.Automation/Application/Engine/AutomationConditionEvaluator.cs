using System.Globalization;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Catalog;

namespace Diaspora.Modules.Automation.Application.Engine;

/// <summary>What one condition decided, and why.</summary>
/// <param name="Matched">Whether the rule may proceed on account of this condition.</param>
/// <param name="Evaluable">
/// False when the platform could not answer it at all. Distinct from a false match: an unevaluable
/// condition makes the run <i>skipped with a reason</i> rather than skipped because the member's rule
/// said no, and the run detail shows which.
/// </param>
/// <param name="Explanation">
/// Why, in plain words. <b>Never the member's actual value when the condition is sensitive.</b> "Your
/// balance condition could not be checked", never "your balance is £412.60" — a run record is a
/// durable log with a longer retention than the data it would be quoting.
/// </param>
public readonly record struct AutomationConditionVerdict(
    bool Matched,
    bool Evaluable,
    string Explanation);

/// <summary>
/// Decides whether a rule's conditions are met, from the trigger's payload and nothing else.
/// <para>
/// <b>The safety property is structural, not defensive.</b> This class parses no expression, compiles
/// nothing, executes nothing and reaches no store. A condition is a catalogue key, one of that key's
/// declared operators, and up to two scalars; evaluation is a dictionary lookup followed by a
/// comparison chosen by a <c>switch</c> over a closed set. There is no input that can make it do
/// anything else, which is a stronger guarantee than any amount of sanitising.
/// </para>
/// <para>
/// <b>Every condition fails closed.</b> An unknown key, an operator the key does not declare, a value
/// that will not parse, or a condition the platform has no source for all produce
/// <c>Matched: false</c>. The alternative — treating an unanswerable condition as satisfied — is the
/// register's stated failure: "a simulation that optimistically matched everything would show a
/// member a success that the real run will not reproduce", and a real run that did it would send a
/// notification the member's rule said not to.
/// </para>
/// <para>
/// <b>Conditions are ANDed and there is no way to write anything else.</b> Not a simplification — an
/// engine that offered OR and grouping would be exposing a rules language to members, which §7
/// forbids, and the frontend's builder has no UI that could produce one.
/// </para>
/// </summary>
public static class AutomationConditionEvaluator
{
    /// <summary>
    /// Evaluates one condition against the trigger's payload.
    /// </summary>
    public static AutomationConditionVerdict Evaluate(
        AutomationConditionRule condition, AutomationTriggerContext context)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(context);

        var definition = AutomationCatalog.FindCondition(condition.Key);

        if (definition is null)
        {
            // A stored rule naming a key the catalogue no longer has. The rule was authored by a
            // client and is untrusted input; a catalogue that shrank must not leave an automation
            // running with one fewer check than its owner wrote.
            return new AutomationConditionVerdict(
                false, false, "This check is no longer one the platform can make.");
        }

        if (!definition.Operators.Contains(condition.Operator, StringComparer.Ordinal))
        {
            return new AutomationConditionVerdict(
                false, false, "This check is set up in a way the platform no longer accepts.");
        }

        // ── The conditions no payload may answer ────────────────────────────────────────────────
        //
        // Read from `ResolvedValues` and never from `Values`, and the separation is the whole control.
        // A balance, an entitlement, a creator standing and an analytics figure are answered by the
        // module that owns them, through a seam, from the automation owner's own identity. Sharing one
        // dictionary would mean any module that publishes an event could decide whether a member's
        // balance condition matched by putting a number in a payload.
        if (definition.ExternalSource is not null)
        {
            if (!context.ResolvedValues.TryGetValue(condition.Key, out var answered) ||
                answered is null)
            {
                // Nothing answered: no source is registered for it in this deployment, or the one
                // that is could not answer. Fails closed with the catalogue's own reason, which is
                // exactly Wave 11's behaviour and is the answer a member can act on.
                return new AutomationConditionVerdict(
                    false,
                    false,
                    definition.UnavailableReason
                    ?? "The platform could not check this, so nothing was done.");
            }

            var externallyMatched = Compare(
                condition.Operator, answered, condition.Value, condition.SecondaryValue);

            return new AutomationConditionVerdict(
                externallyMatched,
                true,
                externallyMatched
                    ? $"The '{condition.Key}' check matched."
                    : $"The '{condition.Key}' check did not match, so nothing was done.");
        }

        if (!definition.IsServerEvaluable)
        {
            return new AutomationConditionVerdict(
                false,
                false,
                definition.UnavailableReason
                ?? "The platform cannot check this yet, so nothing was done.");
        }

        if (!context.Values.TryGetValue(condition.Key, out var actual))
        {
            // Two different absences, and saying the wrong one sends a member to fix a rule that was
            // never wrong.
            //
            // On a real run the trigger simply does not carry this field. A rule that checks a
            // listing's price against a guide publication is a member's mistake rather than a defect,
            // and the honest outcome is a skip that names the check.
            //
            // On a preview it usually means the subject could not be described — no sample subject
            // was given, or nothing could answer for the one that was. Phase 5: for a year every dry
            // run took the first branch, because a simulation's values dictionary was always empty,
            // and members testing a rule on an event's country were told the event trigger does not
            // carry a country. It does.
            return new AutomationConditionVerdict(
                false,
                false,
                context.IsPreview
                    ? $"The '{condition.Key}' check was not tested, because this test run had no real "
                      + "subject to check it against. It will be checked when the automation runs."
                    : $"This trigger does not carry the information the '{condition.Key}' check needs.");
        }

        var matched = Compare(condition.Operator, actual, condition.Value, condition.SecondaryValue);

        // The explanation names the check and never the value, for sensitive conditions and for
        // insensitive ones alike. One rule is easier to keep than two, and the insensitive case loses
        // nothing a member needs.
        return new AutomationConditionVerdict(
            matched,
            true,
            matched
                ? $"The '{condition.Key}' check matched."
                : $"The '{condition.Key}' check did not match, so nothing was done.");
    }

    /// <summary>
    /// Evaluates a whole rule.
    /// <para>
    /// Every condition is evaluated even after one fails, because the run detail shows all of them and
    /// a member debugging a rule that never fires needs to see which checks passed. Short-circuiting
    /// would save a dictionary lookup and cost the whole diagnostic.
    /// </para>
    /// </summary>
    public static IReadOnlyList<(AutomationConditionRule Condition, AutomationConditionVerdict Verdict)>
        EvaluateAll(IEnumerable<AutomationConditionRule> conditions, AutomationTriggerContext context)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        return [.. conditions.Select(condition => (condition, Evaluate(condition, context)))];
    }

    private static bool Compare(
        string @operator, string? actual, string? expected, string? secondary) => @operator switch
    {
        "equals" => Equal(actual, expected),
        "notEquals" => !Equal(actual, expected),
        "includes" => ListOf(expected).Any(candidate => Equal(actual, candidate)),
        "excludes" => !ListOf(expected).Any(candidate => Equal(actual, candidate)),
        "greaterThan" => Numeric(actual) is { } left && Numeric(expected) is { } right && left > right,
        "lessThan" => Numeric(actual) is { } left && Numeric(expected) is { } right && left < right,
        "within" => Numeric(actual) is { } left && Numeric(expected) is { } right && left <= right,
        "between" => Between(actual, expected, secondary),
        "isTrue" => Equal(actual, "true"),
        "isFalse" => Equal(actual, "false"),
        _ => false
    };

    private static bool Equal(string? left, string? right) =>
        left is not null && right is not null &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string[] ListOf(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Parses a comparison operand.
    /// <para>
    /// Invariant culture, always. A member in Lagos and a member in Berlin write "1000.50" and
    /// "1000,50" for the same amount, and a comparison that read the server's culture would give two
    /// different answers to one rule depending on where the process happened to be deployed.
    /// </para>
    /// </summary>
    private static decimal? Numeric(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// <c>between</c> over numbers, and over a wall-clock time-of-day.
    /// <para>
    /// The time-of-day case wraps: "between 22:00 and 07:00" is a member's evening, and a naive
    /// low ≤ x ≤ high would make it never match. Both operands are minutes-since-midnight in the
    /// trigger's own zone, which the consumer resolved before it built the payload.
    /// </para>
    /// </summary>
    private static bool Between(string? actual, string? low, string? high)
    {
        if (Numeric(actual) is { } value && Numeric(low) is { } from && Numeric(high) is { } to)
        {
            return from <= to ? value >= from && value <= to : value >= from || value <= to;
        }

        if (TimeOnly.TryParse(actual, CultureInfo.InvariantCulture, out var at) &&
            TimeOnly.TryParse(low, CultureInfo.InvariantCulture, out var start) &&
            TimeOnly.TryParse(high, CultureInfo.InvariantCulture, out var end))
        {
            return start <= end ? at >= start && at <= end : at >= start || at <= end;
        }

        return false;
    }
}
