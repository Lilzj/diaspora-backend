using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Automation.Application.Mapping;
using Diaspora.Modules.Automation.Domain.Catalog;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The catalogue must not deny what the evaluator can do.
/// <para>
/// <b>Five conditions were reported unavailable to every member for as long as the seam existed.</b>
/// <c>walletBalance</c>, <c>subscriptionActive</c>, <c>creatorStatus</c>, <c>metricAbove</c> and
/// <c>metricBelow</c> carry <c>IsServerEvaluable: false</c> in the static catalogue — correct in
/// Wave 11, when nothing answered them. Wave 12 added <c>IAutomationConditionSource</c>, Financial,
/// Creator and Analytics each claimed it, and <c>AutomationConditionEvaluator</c> has read their
/// answers out of <c>ResolvedValues</c> ever since. The catalogue projection kept reading the static
/// flag, so <c>GET /automations/catalog</c> said "nothing in this deployment answers for it" about
/// conditions this deployment answers.
/// </para>
/// <para>
/// Two correct pieces and one stale reading between them. The check below is that the projection
/// asks rather than assumes — because the failure mode is silent in both directions: a member never
/// offered a check that works, or offered one that cannot.
/// </para>
/// </summary>
public sealed class AutomationCatalogTruthfulnessTests
{
    /// <summary>Every externally-sourced condition, which is the set this is about.</summary>
    private static readonly string[] ExternallySourced =
    [
        .. AutomationCatalog.Conditions.Values
            .Where(condition => condition.ExternalSource is not null)
            .Select(condition => condition.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
    ];

    [Fact]
    public void The_set_this_is_about_is_not_empty()
    {
        // A guard over an empty set passes for the wrong reason, and this one reads a catalogue that
        // could be renamed out from under it.
        ExternallySourced.Should().NotBeEmpty();
    }

    /// <summary>A claimed seam makes its condition available.</summary>
    [Fact]
    public void A_condition_whose_seam_is_claimed_is_offered()
    {
        var catalog = AutomationMapper.Catalog(
            ExternallySourced.ToHashSet(StringComparer.Ordinal));

        foreach (var key in ExternallySourced)
        {
            var entry = catalog.Conditions.Single(condition => condition.Key == key);

            entry.IsAvailable.Should().BeTrue(
                "{0} is answered by a registered source, and telling a member otherwise denies them "
                + "a check the engine would have made", key);

            entry.UnavailableReason.Should().BeNull(
                "a reason attached to an available part is a sentence waiting to be rendered "
                + "somewhere it contradicts the flag beside it");
        }
    }

    /// <summary>And an unclaimed one is still refused, with its reason.</summary>
    [Fact]
    public void A_condition_whose_seam_is_unclaimed_is_refused_with_a_reason()
    {
        var catalog = AutomationMapper.Catalog(new HashSet<string>(StringComparer.Ordinal));

        foreach (var key in ExternallySourced)
        {
            var entry = catalog.Conditions.Single(condition => condition.Key == key);

            entry.IsAvailable.Should().BeFalse(
                "no source claims {0} in this deployment", key);

            entry.UnavailableReason.Should().NotBeNullOrWhiteSpace(
                "an unavailable part without a reason is a dead end for whoever reads it");
        }
    }

    /// <summary>
    /// A condition the evaluator reads from a payload is unaffected by which seams are claimed.
    /// <para>
    /// The two halves of the dictionary are separate on purpose — a payload may never answer an
    /// externally-sourced key, and a seam has nothing to say about whether a trigger carries a
    /// country. A projection that blurred them would be undoing the control.
    /// </para>
    /// </summary>
    [Fact]
    public void A_payload_carried_condition_does_not_move_with_the_seams()
    {
        var withSources = AutomationMapper.Catalog(
            ExternallySourced.ToHashSet(StringComparer.Ordinal));
        var without = AutomationMapper.Catalog(new HashSet<string>(StringComparer.Ordinal));

        foreach (var condition in AutomationCatalog.Conditions.Values
                     .Where(condition => condition.ExternalSource is null))
        {
            var a = withSources.Conditions.Single(entry => entry.Key == condition.Key);
            var b = without.Conditions.Single(entry => entry.Key == condition.Key);

            a.IsAvailable.Should().Be(b.IsAvailable, "{0} is answered from the payload", condition.Key);
        }
    }

    /// <summary>
    /// Every key a condition source answers is a key the catalogue has.
    /// <para>
    /// A source claiming a key nobody offers is the mirror image: work wired to nothing. It would
    /// also make the check above pass over a key that never reaches a member.
    /// </para>
    /// </summary>
    [Fact]
    public void The_seam_vocabulary_and_the_catalogue_agree()
    {
        var declared = new[]
        {
            AutomationConditionKeys.WalletBalance,
            AutomationConditionKeys.SubscriptionActive,
            AutomationConditionKeys.CreatorStatus,
            AutomationConditionKeys.MetricAbove,
            AutomationConditionKeys.MetricBelow
        };

        declared.Should().BeEquivalentTo(
            ExternallySourced,
            "`AutomationConditionKeys` is what a module implements against and the catalogue is what "
            + "a member is offered; a key in one and not the other is a seam with no surface or a "
            + "surface with no seam");
    }
}
