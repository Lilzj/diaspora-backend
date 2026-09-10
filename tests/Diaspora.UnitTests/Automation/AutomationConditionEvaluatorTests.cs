using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Automation;

/// <summary>
/// What a condition decides, and — more importantly — what it decides when it cannot decide.
/// <para>
/// <b>Every path through this evaluator that is not a confident match must be a non-match.</b> An
/// unknown key, an operator the key does not declare, a value that will not parse, a field the
/// trigger does not carry and a condition the platform has no source for are five different ways of
/// not knowing, and all five have to produce the same answer. A single one of them defaulting to
/// matched is a member notified against their own rule.
/// </para>
/// </summary>
public sealed class AutomationConditionEvaluatorTests
{
    // ── The comparisons ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("equals", "NG", "NG", true)]
    [InlineData("equals", "ng", "NG", true)]
    [InlineData("equals", "GB", "NG", false)]
    [InlineData("notEquals", "GB", "NG", true)]
    [InlineData("notEquals", "NG", "NG", false)]
    [InlineData("includes", "NG", "NG,GB,US", true)]
    [InlineData("includes", "FR", "NG,GB,US", false)]
    public void Equality_and_membership_compare_case_insensitively(
        string @operator, string actual, string expected, bool matched)
    {
        var verdict = Evaluate("country", @operator, expected, null, ("country", actual));

        verdict.Matched.Should().Be(matched);
        verdict.Evaluable.Should().BeTrue();
    }

    /// <summary>
    /// Numbers parse under the invariant culture, always.
    /// <para>
    /// A member in Lagos and one in Berlin write "1000.50" and "1000,50" for the same amount. A
    /// comparison that read the server's culture would give two different answers to one rule
    /// depending on where the process happened to be deployed.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("priceAbove", "greaterThan", "1000.50", "500", true)]
    [InlineData("priceAbove", "greaterThan", "100", "500", false)]
    [InlineData("priceBelow", "lessThan", "100.25", "500", true)]
    [InlineData("priceBelow", "lessThan", "900", "500", false)]
    public void Numeric_comparison_uses_the_invariant_culture(
        string key, string @operator, string actual, string expected, bool matched) =>
        Evaluate(key, @operator, expected, null, (key, actual))
            .Matched.Should().Be(matched);

    /// <summary>
    /// A time-of-day range that crosses midnight matches the evening it describes.
    /// <para>
    /// "Between 22:00 and 07:00" is a member's night. A naive low ≤ x ≤ high would never match it,
    /// and the rule would silently do nothing for exactly the members who wrote the most careful one.
    /// </para>
    [Theory]
    [InlineData("23:30", true)]
    [InlineData("03:00", true)]
    [InlineData("12:00", false)]
    [InlineData("21:59", false)]
    public void A_time_range_that_wraps_midnight_matches_the_night(string at, bool matched) =>
        Evaluate("timeOfDay", "between", "22:00", "07:00", ("timeOfDay", at))
            .Matched.Should().Be(matched);

    /// <summary>
    /// <c>between</c> is offered by exactly one condition, and it is a time.
    /// <para>
    /// The evaluator's numeric branch of <c>between</c> is defensive rather than reachable: no
    /// condition in the catalogue declares <c>between</c> over a number, and a condition that did
    /// would be refused on write until the catalogue said so. Asserting the shape here means a future
    /// entry adding a numeric range gets a passing evaluator rather than a silent non-match.
    /// </para>
    /// </summary>
    [Fact]
    public void Between_is_declared_by_exactly_one_condition() =>
        Diaspora.Modules.Automation.Domain.Catalog.AutomationCatalog.Conditions.Values
            .Where(condition => condition.Operators.Contains("between", StringComparer.Ordinal))
            .Select(condition => condition.Key)
            .Should().BeEquivalentTo(["timeOfDay"]);

    // ── The five ways of not knowing ────────────────────────────────────────────────────────

    /// <summary>A key the catalogue does not have fails closed and says the check is gone.</summary>
    [Fact]
    public void An_unknown_condition_key_fails_closed()
    {
        var verdict = Evaluate("not.a.condition", "equals", "x", null, ("country", "NG"));

        verdict.Matched.Should().BeFalse();
        verdict.Evaluable.Should().BeFalse();
    }

    /// <summary>An operator the key does not declare fails closed.</summary>
    [Fact]
    public void An_operator_the_key_does_not_declare_fails_closed()
    {
        // `country` declares equals, notEquals and includes. Not greaterThan.
        var verdict = Evaluate("country", "greaterThan", "NG", null, ("country", "NG"));

        verdict.Matched.Should().BeFalse();
        verdict.Evaluable.Should().BeFalse();
    }

    /// <summary>
    /// A condition the platform has no source for fails closed and quotes the reason.
    /// <para>
    /// This is the one a member is most likely to write: a wallet balance, a subscription state, an
    /// analytics figure. Optimistically matching would show them a rule that fires and does the wrong
    /// thing; failing closed shows them a rule that skips and says why.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("walletBalance")]
    [InlineData("subscriptionActive")]
    [InlineData("creatorStatus")]
    [InlineData("metricAbove")]
    [InlineData("metricBelow")]
    public void A_condition_with_no_source_fails_closed(string key)
    {
        var verdict = Evaluate(
            key,
            key is "subscriptionActive" ? "isTrue" : "greaterThan",
            "1",
            null,
            (key, "999999"));

        verdict.Matched.Should().BeFalse("an unanswerable condition must never let a run through");
        verdict.Evaluable.Should().BeFalse();
        verdict.Explanation.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>A field the trigger does not carry fails closed and names the check.</summary>
    [Fact]
    public void A_field_the_trigger_does_not_carry_fails_closed()
    {
        var verdict = Evaluate("country", "equals", "NG", null, ("category", "housing"));

        verdict.Matched.Should().BeFalse();
        verdict.Evaluable.Should().BeFalse();
        verdict.Explanation.Should().Contain("country");
    }

    /// <summary>A value that will not parse as a number does not match.</summary>
    [Fact]
    public void An_unparseable_number_does_not_match() =>
        Evaluate("priceAbove", "greaterThan", "500", null, ("priceAbove", "not-a-number"))
            .Matched.Should().BeFalse();

    // ── Privacy ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An explanation never quotes the member's value.
    /// <para>
    /// A run record is a durable log with a longer retention than the data it would be quoting. "Your
    /// balance condition matched", never "your balance is £412.60" — and the rule is applied to
    /// insensitive conditions as well, because one rule is easier to keep than two and the
    /// insensitive case loses nothing a member needs.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("amountAbove", "greaterThan", "412.60")]
    [InlineData("applicationStatus", "equals", "Shortlisted")]
    [InlineData("paymentKind", "equals", "deposit")]
    public void An_explanation_never_quotes_the_compared_value(
        string key, string @operator, string value)
    {
        var verdict = Evaluate(key, @operator, value, null, (key, value));

        verdict.Explanation.Should().NotContain(
            value, "a sensitive comparison must not survive in a durable log");

        verdict.Explanation.Should().Contain(key);
    }

    // ── The whole rule ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every condition is evaluated, even after one fails.
    /// <para>
    /// Short-circuiting would save a dictionary lookup and cost the diagnostic: a member debugging a
    /// rule that never fires needs to see which of their checks passed.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_condition_is_evaluated_even_after_one_fails()
    {
        var automation = Draft();

        automation.ReplaceConditions(
        [
            (null, "country", "equals", "GB", null),
            (null, "category", "equals", "housing", null)
        ]);

        var values = AutomationTriggerContext.NewValues();
        values["country"] = "NG";
        values["category"] = "housing";

        var verdicts = AutomationConditionEvaluator.EvaluateAll(automation.Conditions, Context(values));

        verdicts.Should().HaveCount(2);
        verdicts[0].Verdict.Matched.Should().BeFalse();
        verdicts[1].Verdict.Matched.Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    // ── The two absences — Phase 5 ──────────────────────────────────────────────────────────

    /// <summary>
    /// A real run blames the trigger; a preview blames the preview.
    /// <para>
    /// <b>Both are "no value", and telling a member the wrong one sends them to change a rule that
    /// was never wrong.</b> For as long as <c>POST /automations/simulate</c> has existed it built a
    /// trigger context with an empty values dictionary, so every dry run took the first branch — and
    /// a member testing a rule on an event's country was told the event trigger does not carry a
    /// country. Confirmed against the deployed platform on 10 Sep 2026 and fixed with the sample
    /// subject's real state; this pins the wording that made it invisible.
    /// </para>
    /// </summary>
    [Fact]
    public void A_missing_value_on_a_real_run_names_the_trigger()
    {
        var verdict = Evaluate("country", "equals", "NG", null, preview: false);

        verdict.Matched.Should().BeFalse();
        verdict.Evaluable.Should().BeFalse();
        verdict.Explanation.Should().Contain("does not carry");
    }

    [Fact]
    public void A_missing_value_on_a_preview_says_it_was_not_tested()
    {
        var verdict = Evaluate("country", "equals", "NG", null, preview: true);

        verdict.Matched.Should().BeFalse();
        verdict.Evaluable.Should().BeFalse();

        verdict.Explanation.Should().NotContain(
            "does not carry",
            "the trigger carries country perfectly well — it is the test run that had nothing to "
            + "check against, and blaming the trigger sends a member to rewrite a working rule");

        verdict.Explanation.Should().Contain("no real subject");
    }

    /// <summary>A preview with the subject's real state evaluates exactly as the real run does.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_value_that_is_present_is_compared_the_same_either_way(bool preview)
    {
        var verdict = Evaluate(
            "country", "equals", "MU", null, preview, ("country", "MU"));

        verdict.Matched.Should().BeTrue();
        verdict.Evaluable.Should().BeTrue();
    }

    private static AutomationConditionVerdict Evaluate(
        string key,
        string @operator,
        string? value,
        string? secondary,
        bool preview,
        params (string Key, string? Value)[] payload)
    {
        var automation = Draft();
        automation.ReplaceConditions([(null, key, @operator, value, secondary)]);

        var values = AutomationTriggerContext.NewValues();

        foreach (var (name, entry) in payload)
        {
            values[name] = entry;
        }

        return AutomationConditionEvaluator.Evaluate(
            automation.Conditions[0], Context(values) with { IsPreview = preview });
    }

    private static AutomationConditionVerdict Evaluate(
        string key,
        string @operator,
        string? value,
        string? secondary,
        params (string Key, string? Value)[] payload)
    {
        var automation = Draft();
        automation.ReplaceConditions([(null, key, @operator, value, secondary)]);

        var values = AutomationTriggerContext.NewValues();

        foreach (var (name, entry) in payload)
        {
            values[name] = entry;
        }

        return AutomationConditionEvaluator.Evaluate(automation.Conditions[0], Context(values));
    }

    private static AutomationTriggerContext Context(IReadOnlyDictionary<string, string?> values) =>
        new()
        {
            TriggerKey = "manual.run",
            TriggerEventId = "test",
            ConcernedOwnerIds = [],
            Summary = "test",
            OccurredAt = DateTimeOffset.UnixEpoch,
            Values = values
        };

    private static AutomationDefinition Draft() =>
        AutomationDefinition.Create(
            AutomationOwnerKind.User,
            Guid.NewGuid(),
            "You",
            UserId.New(),
            "Test",
            string.Empty,
            AutomationSource.Builder,
            null,
            "manual.run",
            "{}",
            activateImmediately: false,
            DateTimeOffset.UnixEpoch,
            null);
}
