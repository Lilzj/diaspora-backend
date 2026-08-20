using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.UnitTests.Housing.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// Neighbourhood profiles and the moderated insights that hang off them.
/// <para>
/// The tests that matter most here are the ownership ones: a profile keys on a Location area and
/// owns no geography, and nothing a member writes is visible until somebody has read it.
/// </para>
/// </summary>
public class NeighborhoodProfileTests
{
    private static readonly DateTimeOffset Now = HousingFixtures.Now;

    /// <summary>
    /// The boundary the whole design turns on. Housing owns a profile <em>of</em> a neighbourhood;
    /// Location owns the neighbourhood.
    /// </summary>
    [Fact]
    public void A_profile_keys_on_a_location_area()
    {
        var areaId = Guid.NewGuid();
        var profile = HousingFixtures.NeighborhoodProfile(areaId);

        profile.AreaId.Should().Be(areaId);
        profile.Name.Should().Be("Peckham", "cached from Location for rendering");
    }

    [Fact]
    public void A_profile_cannot_be_created_without_a_location_area()
    {
        NeighborhoodProfile.Create(
                Modules.Housing.Domain.Identifiers.NeighborhoodProfileId.From(Guid.NewGuid()),
                Guid.Empty,
                "GB",
                "Nowhere",
                "nowhere",
                Now)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_new_profile_is_empty_and_unapproved()
    {
        var profile = HousingFixtures.NeighborhoodProfile();

        profile.HasContent.Should().BeFalse();
        profile.IsApproved.Should().BeFalse();
        profile.Completeness.Should().Be(0d);
    }

    /// <summary>
    /// Writing is not approving. That is what makes these summaries safe to let a wide set of
    /// contributors edit: the platform publishes prose about the places members live.
    /// </summary>
    [Fact]
    public void Writing_the_narrative_leaves_it_unapproved()
    {
        var profile = HousingFixtures.NeighborhoodProfile();

        var narrative = NeighborhoodNarrative.Create(
            "A busy, friendly part of south London.",
            safetySummary: "Fine in the day; walk the main road at night.").Value;

        profile.UpdateNarrative(narrative, HousingFixtures.NewUserId(), Now)
            .IsSuccess.Should().BeTrue();

        profile.HasContent.Should().BeTrue();
        profile.IsApproved.Should().BeFalse();
    }

    [Fact]
    public void Approving_stamps_the_review_date()
    {
        var profile = HousingFixtures.NeighborhoodProfile();
        var approver = HousingFixtures.NewUserId();

        profile.Approve(approver, Now).IsSuccess.Should().BeTrue();

        profile.IsApproved.Should().BeTrue();
        profile.LastReviewedAt.Should().Be(Now);
        profile.LastReviewedBy.Should().Be(approver);
    }

    /// <summary>
    /// A place being renamed says nothing about whether the prose describing it is still true.
    /// </summary>
    [Fact]
    public void Refreshing_the_cached_geography_does_not_unapprove_the_profile()
    {
        var profile = HousingFixtures.NeighborhoodProfile();

        profile.Approve(HousingFixtures.NewUserId(), Now);
        profile.RefreshPlace("Peckham Rye", "peckham-rye", "London", null, null);

        profile.IsApproved.Should().BeTrue();
        profile.Name.Should().Be("Peckham Rye");
    }

    /// <summary>
    /// Unknown is not a low rating. A neighbourhood nobody has assessed must not sort below one
    /// somebody rated badly.
    /// </summary>
    [Fact]
    public void An_unassessed_index_reads_as_unknown_rather_than_lowest()
    {
        var index = NeighborhoodLivingIndex.Unassessed;

        index.Assessed.Should().BeFalse();
        index.Walkability.Should().Be(NeighborhoodRating.Unknown);
        index.AssessedCount.Should().Be(0);
    }

    [Fact]
    public void Completeness_counts_what_has_actually_been_written()
    {
        var profile = HousingFixtures.NeighborhoodProfile();

        profile.UpdateNarrative(
            NeighborhoodNarrative.Create(
                "A busy part of south London.",
                overview: "The long read.",
                safetySummary: "Fine in the day.").Value,
            null,
            Now);

        profile.UpdateLivingIndex(
            NeighborhoodLivingIndex.Create(
                walkability: NeighborhoodRating.High,
                noiseLevel: NeighborhoodRating.Moderate),
            null,
            Now);

        // Three summaries and two bands, out of sixteen possible.
        profile.Completeness.Should().BeApproximately(5d / 16d, 0.001d);
    }

    /// <summary>
    /// The single most relevant field on the profile for a member deciding whether they will be
    /// understood at the chemist.
    /// </summary>
    [Fact]
    public void Popular_languages_are_ordered_by_prominence()
    {
        var profile = HousingFixtures.NeighborhoodProfile();

        profile.SetPopularLanguages(
            [
                NeighborhoodLanguage.Create("Yoruba", NeighborhoodRating.Moderate),
                NeighborhoodLanguage.Create("English", NeighborhoodRating.VeryHigh),
                NeighborhoodLanguage.Create("Twi", NeighborhoodRating.Low)
            ],
            null,
            Now);

        profile.PopularLanguages.Select(language => language.Name)
            .Should().ContainInOrder("English", "Yoruba", "Twi");
    }

    // ── Insights ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Submission is not publication. An unmoderated free-text field attached to a real residential
    /// area is the most predictable liability a housing platform can build.
    /// </summary>
    [Fact]
    public void A_submitted_insight_is_not_published()
    {
        var profile = HousingFixtures.NeighborhoodProfile();
        var insight = HousingFixtures.Insight(profile);

        insight.Status.Should().Be(NeighborhoodInsightStatus.Submitted);
        insight.IsPublished.Should().BeFalse();
        insight.IsAwaitingReview.Should().BeTrue();
    }

    [Fact]
    public void An_approved_insight_is_published()
    {
        var profile = HousingFixtures.NeighborhoodProfile();
        var insight = HousingFixtures.Insight(profile);

        insight.Approve(HousingFixtures.NewUserId(), null, Now).IsSuccess.Should().BeTrue();

        insight.IsPublished.Should().BeTrue();
    }

    [Fact]
    public void A_rejection_carries_a_reason_the_author_can_read()
    {
        var profile = HousingFixtures.NeighborhoodProfile();
        var insight = HousingFixtures.Insight(profile);

        insight.Reject(HousingFixtures.NewUserId(), "Unverifiable claim.", Now);

        insight.Status.Should().Be(NeighborhoodInsightStatus.Rejected);
        insight.DecisionNote.Should().Be("Unverifiable claim.");
    }

    [Fact]
    public void An_insight_cannot_be_decided_twice()
    {
        var profile = HousingFixtures.NeighborhoodProfile();
        var insight = HousingFixtures.Insight(profile);
        var moderator = HousingFixtures.NewUserId();

        insight.Approve(moderator, null, Now);

        insight.Reject(moderator, null, Now).IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// Retiring is distinct from rejecting: one says "we will not publish this", the other says
    /// "this was right and the place has changed".
    /// </summary>
    [Fact]
    public void Retiring_only_applies_to_something_already_published()
    {
        var profile = HousingFixtures.NeighborhoodProfile();
        var insight = HousingFixtures.Insight(profile);
        var moderator = HousingFixtures.NewUserId();

        insight.Retire(moderator, null, Now).IsFailure.Should().BeTrue(
            "it was never approved");

        insight.Approve(moderator, null, Now);
        insight.Retire(moderator, "The tram line opened.", Now.AddYears(1))
            .IsSuccess.Should().BeTrue();

        insight.Status.Should().Be(NeighborhoodInsightStatus.Retired);
    }

    /// <summary>
    /// Endorsing your own observation adds nothing and inflates the one number the profile ranks by.
    /// </summary>
    [Fact]
    public void An_author_cannot_endorse_their_own_insight()
    {
        var profile = HousingFixtures.NeighborhoodProfile();
        var author = HousingFixtures.NewUserId();
        var insight = HousingFixtures.Insight(profile, author);

        insight.Approve(HousingFixtures.NewUserId(), null, Now);

        insight.Endorse(author, 1, Now).IsSuccess.Should().BeTrue(
            "quietly ignored rather than refused");

        insight.EndorsementCount.Should().Be(0);
    }

    [Fact]
    public void An_unapproved_insight_cannot_be_endorsed()
    {
        var profile = HousingFixtures.NeighborhoodProfile();
        var insight = HousingFixtures.Insight(profile);

        insight.Endorse(HousingFixtures.NewUserId(), 1, Now).IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// The fingerprint catches the same observation retyped with different spacing, which is what
    /// an impatient member does when their first submission has not appeared.
    /// </summary>
    [Fact]
    public void The_fingerprint_collapses_whitespace_and_case()
    {
        NeighborhoodInsight.Fingerprints("Quiet   after  TEN")
            .Should().Be(NeighborhoodInsight.Fingerprints("quiet after ten"));
    }

    [Fact]
    public void An_insight_longer_than_the_limit_is_refused()
    {
        var profile = HousingFixtures.NeighborhoodProfile();

        NeighborhoodInsight.Submit(
                Modules.Housing.Domain.Identifiers.NeighborhoodInsightId.From(Guid.NewGuid()),
                profile.Id,
                profile.AreaId,
                HousingFixtures.NewUserId(),
                NeighborhoodInsightCategory.Safety,
                new string('x', 401),
                null,
                Now)
            .IsFailure.Should().BeTrue();
    }
}
