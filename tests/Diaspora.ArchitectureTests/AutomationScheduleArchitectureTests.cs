using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// A scheduled trigger nobody arms is a promise the platform cannot keep.
/// <para>
/// <c>AutomationCatalogTests.Every_available_trigger_names_a_real_published_contract</c> guards the
/// <see cref="AutomationTriggerDelivery.DomainEvent"/> family: a trigger keyed on a contract that
/// does not exist matches nothing. There was no equivalent for the
/// <see cref="AutomationTriggerDelivery.Schedule"/> family, and that is the hole
/// <c>event.startingSoon</c> shipped through — offered in the builder since Phase 18, reachable in
/// two taps from an event screen, and armed by nothing at all. A member could set a reminder for an
/// event and never be reminded.
/// </para>
/// <para>
/// A scheduled trigger reaches the engine through the subject-horizon store, so the check is that
/// something calls <c>ArmAsync</c> with the trigger's key. Source rather than reflection, because
/// the key is a string argument and no type carries it.
/// </para>
/// </summary>
public sealed class AutomationScheduleArchitectureTests
{
    /// <summary>
    /// Scheduled triggers that are offered and cannot fire, with the reason each is exempt.
    /// <para>
    /// An explicit set rather than a null check, for the reason
    /// <c>AutomationCatalog.MultiSourceTriggers</c> states about its own exemption: it is what stops
    /// a future entry acquiring the exemption silently. Anything added here should be a decision
    /// somebody took, not a gap somebody left.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownUnarmed =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["creator.performanceThreshold"] =
                "Offered to creators and armed by nothing. Firing it needs a scheduled pass over "
                + "creator analytics comparing a metric against a member-set threshold, and which "
                + "metrics and what cadence are product decisions rather than mechanical ones. "
                + "Recorded here so it is visible rather than silent.",
        };

    [Fact]
    public void Every_available_scheduled_trigger_is_armed_by_something()
    {
        var armed = ArmedTriggerKeys();

        var unarmed = AutomationCatalog.Triggers.Values
            .Where(trigger =>
                trigger.IsAvailable &&
                trigger.Delivery == AutomationTriggerDelivery.Schedule)
            .Select(trigger => trigger.Key)
            .Where(key => !armed.Contains(key))
            .Where(key => !KnownUnarmed.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        unarmed.Should().BeEmpty(
            "a scheduled trigger the builder offers and no consumer arms is configured by a member "
            + "and then never fires — which reads to them as the platform forgetting, not as a "
            + "feature that was never wired");
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

        return keys;
    }
}
