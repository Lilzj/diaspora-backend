using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// A scheduled trigger nobody fires is a promise the platform cannot keep.
/// <para>
/// <c>AutomationCatalogTests.Every_available_trigger_names_a_real_published_contract</c> guards the
/// <see cref="AutomationTriggerDelivery.DomainEvent"/> family: a trigger keyed on a contract that
/// does not exist matches nothing. There was no equivalent for the
/// <see cref="AutomationTriggerDelivery.Schedule"/> family, and that is the hole
/// <c>event.startingSoon</c> shipped through — offered in the builder since Phase 18, reachable in
/// two taps from an event screen, and armed by nothing at all.
/// </para>
/// <para>
/// <b>The Schedule family fires two different ways, and the first version of this test knew only
/// one.</b> A <i>horizon</i> trigger is armed by a consumer when a dated subject appears — a job
/// publication writes a row and the row fires a day before the posting closes — so the check is that
/// something calls <c>ArmAsync</c> with its key. An <i>occurrence</i> trigger has no dated subject:
/// it recurs on a schedule the member sets, `AutomationScheduleService.SynchroniseAsync` writes the
/// next occurrence when the rule is saved, and `SweepOccurrencesAsync` runs it. Nothing arms it
/// because there is nothing to arm — the member's own schedule is the arming.
/// </para>
/// <para>
/// <c>creator.performanceThreshold</c> was exempted here as "armed by nothing", which was true of
/// <c>ArmAsync</c> and false of the trigger. It fires from the member's own schedule, which the
/// builder attaches to every <c>delivery: schedule</c> trigger, and its condition is answered by
/// <c>AnalyticsAutomationConditionSource</c>. Every part of it was built. What stopped it was a
/// capability gate, not a missing sweep — see <c>AutomationOwnerReachabilityTests</c>.
/// </para>
/// <para>
/// Source rather than reflection for the arming, because the key is a string argument and no type
/// carries it.
/// </para>
/// </summary>
public sealed class AutomationScheduleArchitectureTests
{
    /// <summary>
    /// Scheduled triggers that are offered and cannot fire, with the reason each is exempt.
    /// <para>
    /// Empty, and that is the finished state. <c>creator.performanceThreshold</c> stood here and was
    /// misfiled: it is an occurrence trigger, and the check below no longer asks occurrence triggers
    /// to be armed. The dictionary stays because the next entry should have to justify itself.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownUnarmed =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Every available trigger that fires on a schedule, either way.</summary>
    private static string[] ScheduledTriggers() =>
    [
        .. AutomationCatalog.Triggers.Values
            .Where(trigger =>
                trigger.IsAvailable &&
                trigger.Delivery == AutomationTriggerDelivery.Schedule)
            .Select(trigger => trigger.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
    ];

    [Fact]
    public void Every_horizon_trigger_is_armed_by_something()
    {
        var armed = ArmedTriggerKeys();

        var unarmed = ScheduledTriggers()
            .Where(AutomationCatalog.SubjectHorizonTriggers.Contains)
            .Where(key => !armed.Contains(key))
            .Where(key => !KnownUnarmed.ContainsKey(key))
            .ToArray();

        unarmed.Should().BeEmpty(
            "a horizon trigger the builder offers and no consumer arms is configured by a member "
            + "and then never fires — which reads to them as the platform forgetting, not as a "
            + "feature that was never wired");
    }

    /// <summary>
    /// Every scheduled trigger is classified, so a new one cannot be neither.
    /// <para>
    /// The failure this prevents is the quiet one: a trigger added to the Schedule family that is
    /// not a horizon and has no member schedule behind it would pass the check above by not being in
    /// the set, which is how an exemption gets acquired without anybody writing one.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_scheduled_trigger_is_a_horizon_or_a_member_schedule()
    {
        // The occurrence family, named so adding to it is a decision rather than a default.
        var memberScheduled = new HashSet<string>(StringComparer.Ordinal)
        {
            "creator.performanceThreshold"
        };

        ScheduledTriggers()
            .Where(key =>
                !AutomationCatalog.SubjectHorizonTriggers.Contains(key) &&
                !memberScheduled.Contains(key))
            .Should().BeEmpty(
                "a scheduled trigger that is neither armed by a consumer nor recurring on the "
                + "member's own schedule has nothing that would ever fire it");

        memberScheduled
            .Where(key => AutomationCatalog.SubjectHorizonTriggers.Contains(key))
            .Should().BeEmpty("a trigger cannot fire both ways; one of the two lists is wrong");
    }

    /// <summary>
    /// <c>SubjectHorizonTriggers</c> lists what is actually armed, and nothing else.
    /// <para>
    /// It had gone stale and nothing noticed, because its own summary named the test it existed for
    /// and no test read it. <c>event.startingSoon</c> acquired a horizon in Phase 5 and the set kept
    /// four entries.
    /// </para>
    /// </summary>
    [Fact]
    public void The_horizon_set_matches_what_is_armed()
    {
        var armed = ArmedTriggerKeys();

        AutomationCatalog.SubjectHorizonTriggers
            .Where(key => !armed.Contains(key))
            .Should().BeEmpty("the set names a trigger nothing arms");

        armed
            .Where(key => AutomationCatalog.FindTrigger(key)?.IsAvailable == true)
            .Where(key => !AutomationCatalog.SubjectHorizonTriggers.Contains(key))
            .Should().BeEmpty("something arms a trigger the set does not name");
    }

    /// <summary>An exemption for a trigger that has since been armed is an exemption nobody removed.</summary>
    [Fact]
    public void No_exemption_outlives_the_gap_it_records()
    {
        var armed = ArmedTriggerKeys();

        KnownUnarmed.Keys
            .Where(armed.Contains)
            .Should().BeEmpty("the trigger is armed now; take it off the list");

        KnownUnarmed.Keys
            .Should().OnlyContain(key => AutomationCatalog.FindTrigger(key) != null,
                "an exemption for a trigger the catalogue no longer has is dead text");

        KnownUnarmed.Values
            .Should().OnlyContain(reason => reason.Length > 80,
                "an exemption without a stated reason is a place to hide things");
    }

    /// <summary>Every trigger key passed to <c>ArmAsync</c> anywhere in the module.</summary>
    private static HashSet<string> ArmedTriggerKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        var consumers = Path.Combine(
            SourceTree.Root.FullName, "src", "Diaspora.Modules.Automation");

        foreach (var path in Directory.EnumerateFiles(consumers, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);

            // `ArmAsync(` then, on the following line in every call site, the quoted trigger key.
            foreach (System.Text.RegularExpressions.Match match in
                     System.Text.RegularExpressions.Regex.Matches(
                         text, @"ArmAsync\(\s*""([a-zA-Z.]+)"""))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        keys.Should().NotBeEmpty("a sweep that parses nothing passes for the wrong reason");

        return keys;
    }
}
