using System.Text.RegularExpressions;
using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Automation.Domain.Catalog;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// A part nobody can own is a part nobody can use.
/// <para>
/// <b><c>community.announcementPublished</c> was served, consumed, matched and unreachable.</b> The
/// contract is published, <c>CommunityAnnouncementAutomationConsumer</c> translates it, and the
/// engine matches on owner id — so an organisation-owned automation would have fired the moment its
/// organisation published an announcement. The catalogue allowed the Community owner kind alone.
/// No module implements <c>IAutomationScopeAuthority</c> for Community: <c>SelfOnlyScopeAuthority</c>
/// answers <c>None()</c> for it and nothing overrides that, because a community on this platform has
/// members and no administrators — roles are an organisation concept. So a complete capability
/// reached nobody, and <c>GET /automations/catalog</c> reported it available throughout.
/// </para>
/// <para>
/// Two correct decisions — a catalogue that names who may own a thing, and a floor authority that
/// grants nothing it cannot justify — meeting at a part neither of them was wrong about.
/// </para>
/// <para>
/// Source rather than reflection for the authorities, because <c>Handles</c> is not the question:
/// <c>SelfOnlyScopeAuthority</c> handles every kind and grants only the member their own. What
/// matters is which kinds a module <i>claims</i>, and that is a declaration in a file.
/// </para>
/// </summary>
public sealed class AutomationOwnerReachabilityTests
{
    /// <summary>
    /// Parts allowed only to an owner kind nobody grants, with the reason each is exempt.
    /// <para>
    /// An explicit list, for the reason the schedule guard states about its own: it is what stops a
    /// future entry acquiring the exemption in silence.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownUnownable =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Empty, and that is the finished state rather than an unfilled one.
            //
            // `notify.community` stood here. It is now `IsAvailable: false` with the platform's own
            // reason — a community has members and nobody who may address them, which is what
            // `PublishAnnouncementCommandHandler` says when it refuses every announcement scope but
            // Organization. An unavailable part is not advertised, so it needs no exemption; the
            // exemption was recording the wrong thing about it.
            //
            // The dictionary stays because the next entry should have to justify itself.
        };

    /// <summary>The kinds a member can actually be granted today.</summary>
    private static IReadOnlySet<string> GrantableOwnerKinds()
    {
        // The floor grants a member their own record for every kind except the three that describe a
        // group somebody administers rather than a role somebody plays. That exclusion is written in
        // `SelfOnlyScopeAuthority` and repeated here rather than parsed, because it is the one thing
        // in this check that is a rule rather than a registration.
        var granted = Enum.GetValues<AutomationOwnerKind>()
            .Select(kind => kind.ToString())
            .Where(kind => kind is not ("Community" or "Organization" or "Platform"))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var kind in ClaimedByAModule())
        {
            granted.Add(kind);
        }

        return granted;
    }

    /// <summary>Owner kinds some module claims through its own scope authority.</summary>
    private static IEnumerable<string> ClaimedByAModule()
    {
        var source = Path.Combine(SourceTree.Root.FullName, "src");

        foreach (var path in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            // The floor is not a claim: it handles every kind and grants almost none of them.
            if (Path.GetFileName(path) == "SelfOnlyScopeAuthority.cs")
            {
                continue;
            }

            var text = File.ReadAllText(path);

            if (!text.Contains("IAutomationScopeAuthority", StringComparison.Ordinal))
            {
                continue;
            }

            var declaration = Regex.Match(text, @"Handles \{ get; \} = \[([^\]]*)\]");

            if (!declaration.Success)
            {
                continue;
            }

            foreach (Match kind in Regex.Matches(
                         declaration.Groups[1].Value, @"AutomationOwnerKind\.([A-Za-z]+)"))
            {
                yield return kind.Groups[1].Value;
            }
        }
    }

    [Fact]
    public void The_sweep_finds_the_authorities_it_reads()
    {
        // A probe that parses nothing passes for the wrong reason, and this one reads a declaration
        // shape a formatter could change.
        ClaimedByAModule().Should().Contain(
            "Organization", "CommunityLife claims it in `OrganizationAutomationAuthority`");
    }

    /// <summary>
    /// Community is granted now, by the module that knows who runs one.
    /// <para>
    /// This was <c>Community_is_still_granted_by_nobody</c>, the premise every community exemption
    /// rested on, written to fail the day a module claimed the kind. It failed on 11 Sep 2026, as
    /// designed: communities gained an Owner and administrators, and
    /// <c>CommunityAutomationAuthority</c> answers for them. The exemptions had already gone; the
    /// premise goes with them, and the grant is pinned instead.
    /// </para>
    /// </summary>
    [Fact]
    public void Community_is_granted_by_the_module_that_knows_who_runs_one()
    {
        GrantableOwnerKinds().Should().Contain(
            "Community",
            "CommunityProgramme claims it in `CommunityAutomationAuthority`, and without it every "
            + "community-owned part is reachable by nobody again");
    }

    [Fact]
    public void Every_available_trigger_can_be_owned_by_somebody()
    {
        Unownable(
            AutomationCatalog.Triggers.Values
                .Where(trigger => trigger.IsAvailable)
                .Select(trigger => (trigger.Key, trigger.AllowedOwners)))
            .Should().BeEmpty(
                "a trigger the platform can observe and no member can own is a capability that is "
                + "finished, served, and reaches nobody");
    }

    [Fact]
    public void Every_available_action_can_be_owned_by_somebody()
    {
        Unownable(
            AutomationCatalog.Actions.Values
                .Where(action => action.IsAvailable)
                .Select(action => (action.Key, action.AllowedOwners)))
            .Should().BeEmpty(
                "an action the platform can perform and no member can own reaches nobody");
    }

    /// <summary>
    /// A capability nobody can hold gates as absolutely as an owner kind nobody can be.
    /// <para>
    /// <c>community.announcementPublished</c> required <c>automations.community</c>, and the three
    /// grants in this platform are <c>SelfOnlyScopeAuthority.PersonalCapabilities</c>,
    /// CommunityLife's <c>automations.organization</c> and Creator's <c>automations.creator</c>.
    /// Nothing grants the community one. The same defect as the owner kind, one layer down, from
    /// the same cause — and it would have kept the trigger unreachable after the owner list was
    /// widened, which is exactly the half-finished opening that reads as finished.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_available_part_requires_a_capability_somebody_is_granted()
    {
        var granted = GrantedCapabilities();

        granted.Should().NotBeEmpty("a sweep that parses no grants passes for the wrong reason");

        var unobtainable = AutomationCatalog.Triggers.Values
            .Where(trigger => trigger.IsAvailable)
            .Select(trigger => (trigger.Key, trigger.RequiredCapability))
            .Concat(AutomationCatalog.Actions.Values
                .Where(action => action.IsAvailable)
                .Select(action => (action.Key, action.RequiredCapability)))
            .Where(part => part.RequiredCapability is not null)
            .Where(part => !granted.Contains(part.RequiredCapability!))
            .Where(part => !KnownUnownable.ContainsKey(part.Key))
            .Select(part => $"{part.Key} needs {part.RequiredCapability}")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();

        unobtainable.Should().BeEmpty(
            "a part gated on a capability no authority grants is refused for every member, however "
            + "the owner kinds are set");
    }

    /// <summary>
    /// Every capability string some scope authority actually hands out.
    /// <para>
    /// <b>The first version of this could not fail.</b> It read any file mentioning
    /// <c>IAutomationScopeAuthority</c>, and <c>AutomationCatalog.cs</c> mentions it in a comment
    /// while naming every capability in the platform — as a <i>requirement</i>, which is the
    /// opposite of a grant. So the catalogue vouched for itself and
    /// <c>automations.community</c> looked granted. Caught by reintroducing the defect and watching
    /// the guard pass.
    /// </para>
    /// <para>
    /// A grant is a construction of <see cref="AutomationScopeGrant"/>, so that is what this looks
    /// for, with comments stripped first.
    /// </para>
    /// </summary>
    private static IReadOnlySet<string> GrantedCapabilities()
    {
        var granted = new HashSet<string>(StringComparer.Ordinal);
        var source = Path.Combine(SourceTree.Root.FullName, "src");

        foreach (var path in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            var code = WithoutComments(File.ReadAllText(path));

            if (!code.Contains("new AutomationScopeGrant(", StringComparison.Ordinal))
            {
                continue;
            }

            // Both spellings a grant takes: a bare literal, and the shared constant.
            foreach (Match literal in Regex.Matches(code, @"""(automations\.[a-z]+)"""))
            {
                granted.Add(literal.Groups[1].Value);
            }

            foreach (Match named in Regex.Matches(code, @"AutomationCapabilities\.([A-Za-z]+)"))
            {
                granted.Add($"automations.{named.Groups[1].Value.ToLowerInvariant()}");
            }
        }

        return granted;
    }

    private static string WithoutComments(string text) =>
        Regex.Replace(
            Regex.Replace(text, @"/\*[\s\S]*?\*/", " "),
            @"//[^\n]*", " ");
    [Fact]
    public void No_exemption_outlives_the_gap_it_records()
    {
        var granted = GrantableOwnerKinds();

        foreach (var (key, reason) in KnownUnownable)
        {
            reason.Length.Should().BeGreaterThan(
                80, "an exemption without a stated reason is a place to hide things");

            var owners =
                AutomationCatalog.FindAction(key)?.AllowedOwners
                ?? AutomationCatalog.FindTrigger(key)?.AllowedOwners;

            owners.Should().NotBeNull(
                "an exemption for a key the catalogue no longer has is dead text");

            owners!.Select(owner => owner.ToString())
                .Any(granted.Contains)
                .Should().BeFalse("{0} is ownable now; take it off the list", key);
        }
    }

    private static string[] Unownable(
        IEnumerable<(string Key, IReadOnlyList<AutomationOwnerKind> Owners)> parts)
    {
        var granted = GrantableOwnerKinds();

        return
        [
            .. parts
                .Where(part => part.Owners.Count > 0)
                .Where(part => !part.Owners.Select(owner => owner.ToString()).Any(granted.Contains))
                .Select(part => part.Key)
                .Where(key => !KnownUnownable.ContainsKey(key))
                .OrderBy(key => key, StringComparer.Ordinal)
        ];
    }
}
