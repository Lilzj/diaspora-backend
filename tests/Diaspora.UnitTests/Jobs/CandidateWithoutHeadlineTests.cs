using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.UnitTests.Jobs.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Jobs;

/// <summary>
/// A candidate profile with nothing written on it — 14 Sep 2026.
/// <para>
/// Applying for a job used to require a candidate profile with a headline, so a signed-in member
/// was sent away to write one before any employer received anything. A first application now
/// makes the profile itself, empty. These pin the domain half of that: a blank headline is a
/// valid "none" rather than an error, the empty profile is private and claims nothing, and the
/// employer's row names the applicant from their account instead of leaning on a headline.
/// </para>
/// </summary>
public sealed class CandidateWithoutHeadlineTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_headline_is_no_headline_rather_than_an_error(string? headline)
    {
        var result = CandidateHeadline.Create(headline);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsStated.Should().BeFalse();
        result.Value.Headline.Should().BeEmpty();
    }

    [Fact]
    public void A_headline_that_is_too_long_is_still_refused() =>
        CandidateHeadline.Create(new string('a', 201)).IsFailure.Should().BeTrue();

    [Fact]
    public void A_written_headline_is_stated_and_tidied()
    {
        var headline = CandidateHeadline.Create("  Care   assistant,  3 years ").Value;

        headline.IsStated.Should().BeTrue();
        headline.Headline.Should().Be("Care assistant, 3 years");
    }

    [Fact]
    public void A_profile_made_by_a_first_application_is_private_and_claims_nothing()
    {
        var profile = CandidateProfile.Create(
            CandidateProfileId.New(),
            JobFixtures.NewUserId(),
            CandidateHeadline.None,
            countryCode: null,
            JobFixtures.Now).Value;

        profile.Visibility.Should().Be(CandidateVisibility.Private);
        profile.Headline.IsStated.Should().BeFalse();
        profile.CompletenessPercent.Should().Be(0, "no headline is no longer worth the twenty points one is");

        profile.DomainEvents.OfType<CandidateProfileCreatedDomainEvent>()
            .Single().Headline.Should().BeNull();
    }

    [Fact]
    public void A_written_headline_still_counts_towards_completeness() =>
        JobFixtures.Candidate().CompletenessPercent.Should().Be(20);

    [Fact]
    public void A_city_given_when_the_profile_is_created_is_kept()
    {
        var profile = CandidateProfile.Create(
            CandidateProfileId.New(),
            JobFixtures.NewUserId(),
            CandidateHeadline.None,
            "MU",
            JobFixtures.Now,
            cityName: "  Curepipe ").Value;

        profile.CityName.Should().Be("Curepipe");
    }

    [Fact]
    public void The_employers_row_names_the_applicant_and_leaves_out_a_blank_headline()
    {
        var card = ApplicationMapper.ToCard(
            JobFixtures.Application(),
            "Care assistant, Curepipe",
            "care-assistant-curepipe",
            "Okonkwo Consulting Mauritius",
            JobFixtures.Now,
            candidateHeadline: string.Empty,
            applicant: new MemberProfile { UserId = Guid.NewGuid(), DisplayName = "David Mensah", IsVerified = true });

        card.ApplicantName.Should().Be("David Mensah");
        card.ApplicantIsVerified.Should().BeTrue();
        card.CandidateHeadline.Should().BeNull();
    }

    [Fact]
    public void A_row_with_no_applicant_to_show_says_nothing_about_verification()
    {
        // The candidate's own tracker, or an account that can no longer be found. Null, not false:
        // an unanswered question is not "unverified".
        var card = ApplicationMapper.ToCard(
            JobFixtures.Application(), "Porter", "porter", "An employer", JobFixtures.Now);

        card.ApplicantName.Should().BeNull();
        card.ApplicantIsVerified.Should().BeNull();
    }
}
