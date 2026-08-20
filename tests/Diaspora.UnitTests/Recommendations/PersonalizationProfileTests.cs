using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Events;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Recommendations.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Recommendations;

/// <summary>
/// What the platform believes, and how it comes to believe it.
/// <para>
/// <b>The rebuild test is the one that matters most.</b> A rebuild recomputes what was inferred and
/// must never discard what the member said — and the aggregate is where that is enforced, so a
/// rebuild written later by somebody who has not read the guide still cannot get it wrong.
/// </para>
/// </summary>
public class PersonalizationProfileTests
{
    // ── Affinity ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Affinity saturates rather than growing without limit.</b> A running total would mean a
    /// member who read forty housing guides during one stressful fortnight was permanently classified
    /// as house-hunting; a bounded value lets recency move it back down.
    /// </summary>
    [Fact]
    public void An_affinity_never_exceeds_one_however_often_it_is_observed()
    {
        var profile = Blank();

        for (var i = 0; i < 500; i++)
        {
            profile.ObserveInterest(
                InterestKind.Category, "housing", 1m, RecommendationTestData.Now, Guid.NewGuid());
        }

        profile.AffinityFor(InterestKind.Category, "housing").Should().BeLessThanOrEqualTo(1m);
    }

    /// <summary>One observation moves an affinity a little, not to the ceiling.</summary>
    [Fact]
    public void One_observation_moves_an_affinity_only_a_little()
    {
        var profile = Blank();

        profile.ObserveInterest(
            InterestKind.Category, "housing", 1m, RecommendationTestData.Now, Guid.NewGuid());

        profile.AffinityFor(InterestKind.Category, "housing")
            .Should().BeGreaterThan(0m).And.BeLessThan(0.3m);
    }

    /// <summary>
    /// <b>An observation at zero pulls an affinity down.</b> This is how a dismissal corrects a
    /// profile — gently, so one accidental swipe cannot erase a year of evidence.
    /// </summary>
    [Fact]
    public void An_observation_at_zero_reduces_an_affinity()
    {
        var profile = Blank();

        for (var i = 0; i < 30; i++)
        {
            profile.ObserveInterest(
                InterestKind.Category, "housing", 1m, RecommendationTestData.Now, Guid.NewGuid());
        }

        var before = profile.AffinityFor(InterestKind.Category, "housing");

        profile.ObserveInterest(
            InterestKind.Category, "housing", 0m, RecommendationTestData.Now, Guid.NewGuid());

        profile.AffinityFor(InterestKind.Category, "housing").Should().BeLessThan(before);
    }

    // ── Explicit interests ───────────────────────────────────────────────

    /// <summary>
    /// <b>A stated interest survives a rebuild.</b> A rebuild recomputes what the platform inferred;
    /// discarding what the member said would be a privacy failure dressed as a refresh.
    /// </summary>
    [Fact]
    public void A_rebuild_keeps_what_the_member_said_and_discards_what_was_inferred()
    {
        var profile = Blank();

        profile.DeclareInterest(
            InterestKind.Category, "immigration", RecommendationTestData.Now, Guid.NewGuid());

        profile.ObserveInterest(
            InterestKind.Category, "housing", 1m, RecommendationTestData.Now, Guid.NewGuid());

        profile.BeginRebuild();

        profile.Interests.Should().ContainSingle()
            .Which.Key.Should().Be("immigration");
    }

    /// <summary>A stated interest is exempt from decay for the same reason.</summary>
    [Fact]
    public void A_stated_interest_does_not_decay()
    {
        var profile = Blank();

        profile.DeclareInterest(
            InterestKind.Category, "immigration", RecommendationTestData.Now, Guid.NewGuid());

        var before = profile.AffinityFor(InterestKind.Category, "immigration");

        profile.Decay(0.1m);

        profile.AffinityFor(InterestKind.Category, "immigration").Should().Be(before);
    }

    // ── Location ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A member who moves stops being primarily where they were.</b> Leaving two primaries at one
    /// grain would make the location strategy score both at its ceiling — so somebody who moved from
    /// Peckham to Grand Baie would keep seeing Peckham listings at full weight.
    /// </summary>
    [Fact]
    public void Moving_replaces_the_primary_neighbourhood()
    {
        var profile = Blank();

        profile.SetLocation(
            "GB", "London", "Peckham", RecommendationTestData.Now, (_, _) => Guid.NewGuid());

        profile.SetLocation(
            "MU", "Port Louis", "Grand Baie", RecommendationTestData.Now, (_, _) => Guid.NewGuid());

        profile.LocationAffinities
            .Where(affinity => affinity.Scope == AffinityScope.Neighborhood)
            .Should().ContainSingle(affinity => affinity.IsPrimary);

        profile.PrimaryNeighborhoodName.Should().Be("Grand Baie");
    }

    /// <summary>
    /// <b>The old place keeps a derived affinity.</b> Somebody who spent a year in London still cares
    /// about London, which is the ordinary case for this platform's members rather than an edge one.
    /// </summary>
    [Fact]
    public void A_previous_country_keeps_an_affinity()
    {
        var profile = Blank();

        profile.SetLocation("GB", null, null, RecommendationTestData.Now, (_, _) => Guid.NewGuid());
        profile.SetLocation("MU", null, null, RecommendationTestData.Now, (_, _) => Guid.NewGuid());

        profile.LocationAffinityFor(AffinityScope.Country, "GB").Should().BeGreaterThan(0m);
        profile.LocationAffinityFor(AffinityScope.Country, "MU").Should().Be(1m);
    }

    // ── Cold start ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>Cold start is about evidence, not about fields.</b> A member whose country arrived from
    /// Identity has 25% completeness and still nothing the platform has seen them do.
    /// </summary>
    [Fact]
    public void A_member_with_a_country_and_no_history_is_still_cold()
    {
        var profile = Blank();

        profile.SetLocation("MU", null, null, RecommendationTestData.Now, (_, _) => Guid.NewGuid());
        profile.RecordSignalsFolded(1, RecommendationTestData.Now, RecommendationTestData.Now);

        profile.Completeness.Should().BeGreaterThan(0);
        profile.IsColdStart.Should().BeTrue();
    }

    [Fact]
    public void A_member_with_interests_and_signals_is_no_longer_cold()
    {
        var profile = Blank();

        profile.SetLocation("MU", null, null, RecommendationTestData.Now, (_, _) => Guid.NewGuid());

        profile.ObserveInterest(
            InterestKind.Category, "housing", 1m, RecommendationTestData.Now, Guid.NewGuid());

        profile.RecordSignalsFolded(50, RecommendationTestData.Now, RecommendationTestData.Now);

        profile.IsColdStart.Should().BeFalse();
    }

    // ── Ceilings ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A burst of noise cannot displace established affinity.</b> The ceiling refuses new
    /// interests rather than evicting the weakest, which is what stops a year's evidence being
    /// pushed out by an afternoon of it.
    /// </summary>
    [Fact]
    public void The_interest_ceiling_refuses_new_keys_rather_than_evicting_old_ones()
    {
        var profile = Blank();

        for (var i = 0; i < PersonalizationProfile.MaximumInterests + 50; i++)
        {
            profile.ObserveInterest(
                InterestKind.Tag, $"tag-{i}", 1m, RecommendationTestData.Now, Guid.NewGuid());
        }

        profile.Interests.Should().HaveCount(PersonalizationProfile.MaximumInterests);
        profile.Interests.Should().Contain(interest => interest.Key == "tag-0");
    }

    // ── Events ───────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The published event carries shape and never content.</b> A profile is the most inferential
    /// record the platform holds; putting its interests on a bus every module reads would make this
    /// module the platform's largest unnecessary disclosure surface.
    /// </summary>
    [Fact]
    public void The_update_event_carries_no_interests()
    {
        var profile = Blank();

        profile.ObserveInterest(
            InterestKind.Category, "housing", 1m, RecommendationTestData.Now, Guid.NewGuid());

        profile.CompleteRebuild(RecommendationTestData.Now);

        var raised = profile.DomainEvents
            .OfType<PersonalizationProfileUpdatedDomainEvent>()
            .Should().ContainSingle().Subject;

        raised.WasRebuilt.Should().BeTrue();

        typeof(PersonalizationProfileUpdatedDomainEvent)
            .GetProperties()
            .Should().NotContain(
                property => property.Name.Contains("Interest", StringComparison.Ordinal) ||
                            property.Name.Contains("Affinity", StringComparison.Ordinal),
                "what a member is interested in is not a fact for the bus");
    }

    private static PersonalizationProfile Blank() =>
        PersonalizationProfile.Open(
            PersonalizationProfileId.New(), UserId.New(), RecommendationTestData.Now);
}
