using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The naming conventions of the composed authorisation catalogues, asserted against the catalogues
/// rather than against the source that declares them.
/// <para>
/// <b>The analyzer sees a declaration; this sees the platform.</b> <c>DSP5002</c> and its siblings
/// read the first argument of a <c>new PermissionDefinition(…)</c> and resolve it through the
/// semantic model, which covers everything written that way and nothing written any other way — a
/// module that composed a key at runtime, or built its definitions through a helper taking the key
/// as a parameter, would be invisible to every one of them. Composing the real catalogues and
/// asserting on the strings that come out closes that gap, and it is how B1.4.1 established that
/// all 222 policy names are lower case rather than that the ones the analyzer can read are.
/// </para>
/// <para>
/// <b>Two of these tests exist to keep deferred findings deferred.</b> F1 and F2 name permission
/// keys that are live public contracts — minted into access tokens, held in the frontend's
/// capability map — so B1.4.1 left them alone on purpose. Pinning their exact extent means a future
/// phase that renames one has to come here and say so, and a phase that adds a twenty-sixth key in
/// the old shape finds out on the way in.
/// </para>
/// </summary>
public sealed class AuthorizationConventionArchitectureTests
{
    /// <summary>
    /// <b>F3, closed.</b> B13 and B14 shipped <c>Notifications.Recipient</c> and
    /// <c>Recommendations.Member</c> while the other thirteen modules wrote their policies in lower
    /// case; <c>DSP5006</c> reported it and B1.4 recorded it as F3. Policy names are backend-only —
    /// no token carries one and no client holds one — which is what made this the one naming finding
    /// safe to close without a versioned release.
    /// </summary>
    [Fact]
    public void Every_policy_name_is_lower_case()
    {
        var offenders = AuthorizationModel.Policies
            .Where(policy => !string.Equals(
                policy.Name, policy.Name.ToLowerInvariant(), StringComparison.Ordinal))
            .Select(policy => $"{policy.Module} — {policy.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "policy names are lower case, dotted and backend-only:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// Shape as well as casing: dotted, at least two segments, nothing exotic inside one. The
    /// namespacing is what keeps two modules from both declaring <c>manage</c>.
    /// </summary>
    [Fact]
    public void Every_policy_name_is_dotted_and_namespaced()
    {
        var offenders = AuthorizationModel.Policies
            .Where(policy => !Regex.IsMatch(
                policy.Name, "^[a-z][a-z0-9-]*(\\.[a-z0-9][a-z0-9-]*)+$", RegexOptions.None, TimeSpan.FromSeconds(2)))
            .Select(policy => $"{policy.Module} — {policy.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "a policy name that is not namespaced can collide with another module's:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>The rename that F3 closed is inert, and this is what makes it inert.</b> Both names
    /// resolve to one definition, so an endpoint, a test fixture or a branch that has not merged yet
    /// still authorises exactly as it did. The failure this prevents is silent: an unregistered
    /// policy does not throw, ASP.NET Core denies the request, and the log says nothing about why.
    /// </summary>
    [Fact]
    public void Every_legacy_policy_name_still_resolves_to_its_policy()
    {
        AuthorizationModel.PolicyAliases.Should().HaveCount(
            6, "B13's three policies and B14's three were renamed in B1.4.1 and each kept its old name");

        foreach (var (alias, policy) in AuthorizationModel.PolicyAliases)
        {
            AuthorizationModel.FindPolicy(alias).Should().BeSameAs(
                policy,
                "'{0}' is a second name for '{1}', not a second policy — one definition, so the two " +
                "cannot come to mean different things",
                alias,
                policy.Name);
        }
    }

    /// <summary>
    /// An alias is a name in the same namespace as every other name. One that shadowed a live policy
    /// would hand one module's endpoints another module's requirements, which is a privilege change
    /// disguised as a rename.
    /// </summary>
    [Fact]
    public void No_legacy_policy_name_shadows_a_live_policy()
    {
        var canonical = AuthorizationModel.Policies
            .Select(policy => policy.Name)
            .ToHashSet(StringComparer.Ordinal);

        AuthorizationModel.PolicyAliases
            .Where(entry => canonical.Contains(entry.Alias))
            .Should().BeEmpty("an alias that is also a policy is two rules under one name");

        AuthorizationModel.PolicyAliases
            .Select(entry => entry.Alias)
            .Should().OnlyHaveUniqueItems("two policies answering to one old name is ambiguous");
    }

    /// <summary>
    /// <b>Aliases are a migration mechanism, not a naming style.</b> Every one on the platform is a
    /// name a module used to have; a module reaching for one to give a policy a second permanent
    /// name would be using the transition path as a habit.
    /// </summary>
    [Fact]
    public void Legacy_policy_names_are_only_the_ones_B1_4_1_renamed()
    {
        AuthorizationModel.PolicyAliases
            .Select(entry => entry.Alias)
            .Should().BeEquivalentTo(
                [
                    "Notifications.Recipient",
                    "Notifications.TemplateAuthor",
                    "Notifications.Operator",
                    "Recommendations.Member",
                    "Recommendations.Curator",
                    "Recommendations.Analyst"
                ],
                "deleting these is a decision of its own, and a new one appearing here is a rename " +
                "somebody should be told about");
    }

    /// <summary>
    /// <b>F4, closed against the catalogue rather than against a comment.</b>
    /// <c>PermissionDefinition</c>'s documentation said <c>module:resource:action</c> and eighty-eight
    /// shipped keys have two segments, all of them reasonable — the resource segment adds nothing when
    /// the module protects one thing. B1.4.1 corrected the comment. This is the rule the comment now
    /// describes: colon-separated, lower case, two segments or more.
    /// </summary>
    [Fact]
    public void Every_permission_key_is_lower_case_and_at_least_two_segments()
    {
        var offenders = AuthorizationModel.Permissions
            .Where(permission => !Regex.IsMatch(
                permission.Key,
                "^[a-z][a-z0-9.-]*(:[a-z0-9][a-z0-9-]*)+$",
                RegexOptions.None,
                TimeSpan.FromSeconds(2)))
            .Select(permission => $"{permission.Module} — {permission.Key}")
            .ToList();

        offenders.Should().BeEmpty(
            "a permission key is minted into access tokens; the shape is a contract:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// The counterpart to the rule above, so the documented two-segment form stays documented
    /// because the platform uses it rather than because somebody wrote it down once.
    /// </summary>
    [Fact]
    public void The_documented_key_shapes_are_all_in_use()
    {
        var segments = AuthorizationModel.Permissions
            .GroupBy(permission => Math.Min(permission.Key.Split(':').Length, 4))
            .ToDictionary(group => group.Key, group => group.Count());

        segments.Should().ContainKey(2, "module:action — bookmarks:read, content:publish");
        segments.Should().ContainKey(3, "module:resource:action — marketplace:listings:create");
        segments.Should().ContainKey(
            4, "module:resource:action:scope — community-life:event:edit:own");
    }

    /// <summary>
    /// <b>F1, still deferred, and pinned so it stays a known quantity.</b> The Communication and
    /// Recommendation platforms declare twenty-five keys with a dotted sub-module segment —
    /// <c>notifications.preferences:read</c> — where every other module uses colons throughout.
    /// <para>
    /// Not renamed in B1.4.1 and not renameable outside a versioned authorisation release: these
    /// strings are minted into access tokens, held in the frontend's capability map and stored in
    /// issued sessions. What this asserts is that the set has not grown. A twenty-sixth key in the
    /// old shape is a new module copying a form the platform has already decided against.
    /// </para>
    /// </summary>
    [Fact]
    public void The_deferred_dotted_permission_keys_are_still_only_B13_and_B14()
    {
        var dotted = AuthorizationModel.Permissions
            .Where(permission => permission.Key.Split(':')[0].Contains('.', StringComparison.Ordinal))
            .ToList();

        dotted.Select(permission => permission.Module).Distinct()
            .Should().BeEquivalentTo(
                ["Notifications", "Recommendations"],
                "F1 is B13's and B14's, and a third module joining them is a new finding rather " +
                "than the old one");

        dotted.Should().HaveCount(
            25,
            "F1 covers a fixed set — twelve from B13 and thirteen from B14. A new key in this shape " +
            "is copied from the wrong example; write module:resource:action.");
    }

    /// <summary>
    /// <b>F2, still deferred, with its extent corrected.</b> B1.4 recorded five keys ending in
    /// <c>:view</c> where the platform's vocabulary says <c>read</c>. There are seven — B12's
    /// <c>community-life:analytics:view</c> and B8.1's <c>guides:knowledge:analytics:view</c> were
    /// missed, both declared before B1.4 and both reported by <c>DSP5004</c>, which is an <c>Info</c>
    /// diagnostic and therefore absent from build output.
    /// <para>
    /// Still not renamed, for F1's reason: shipped tokens hold these strings. Counted here so the
    /// number in the readiness record is the number on the platform, and so an eighth cannot arrive
    /// quietly.
    /// </para>
    /// </summary>
    [Fact]
    public void The_deferred_view_permission_keys_are_still_exactly_seven()
    {
        AuthorizationModel.Permissions
            .Select(permission => permission.Key)
            .Where(key => key.EndsWith(":view", StringComparison.Ordinal))
            .Should().BeEquivalentTo(
                [
                    "community-life:analytics:view",
                    "guides:analytics:view",
                    "guides:drafts:view",
                    "guides:knowledge:analytics:view",
                    "housing:analytics:view",
                    "jobs:analytics:view",
                    "marketplace:analytics:view"
                ],
                "F2 is a closed set awaiting a versioned release. New keys use ':read' — DSP5004 " +
                "says so as they are typed.");
    }

    /// <summary>
    /// Every policy names a permission that exists, or a tier, and never neither. The catalogue
    /// already refuses to compose otherwise — this asserts the catalogue composed, which is the
    /// cheapest possible statement that the platform's authorisation wiring is consistent.
    /// </summary>
    [Fact]
    public void Every_policy_resolves_to_a_declared_permission_or_a_tier()
    {
        var keys = AuthorizationModel.Permissions
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var policy in AuthorizationModel.Policies)
        {
            (policy.Permission is not null || policy.MinimumTier is not null).Should().BeTrue(
                "policy '{0}' would authorise every authenticated caller", policy.Name);

            if (policy.Permission is { } permission)
            {
                keys.Should().Contain(
                    permission,
                    "policy '{0}' names a permission no module declares, so every endpoint behind " +
                    "it denies every caller",
                    policy.Name);
            }
        }
    }
}
