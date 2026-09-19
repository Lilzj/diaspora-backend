using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// What happens to a country standing when the member moves away.
/// <para>
/// <b>None of this ran before relocation was wired.</b> <c>CountryStanding.RecordDeparture</c>
/// existed with no caller anywhere in the solution, so every rule below was a claim the code
/// made and nothing checked. The tests are written around the one question that decides whether
/// moving is safe to offer at all: does leaving a country cost the member anything they earned?
/// It must not. Seven years of standing in Mauritius is a fact about the past, and moving to the
/// UK does not edit the past.
/// </para>
/// </summary>
public sealed class CountryStandingDepartureTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member =
        UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private static readonly DateOnly Arrived = new(2019, 3, 1);
    private static readonly DateOnly Departed = new(2025, 6, 30);

    /// <summary>A verified resident of Mauritius who has not left.</summary>
    private static TrustProfile VerifiedInMauritius()
    {
        var profile = TrustProfile.Open(Member, Now);
        profile.RecordGrant(VerificationType.Country, "MU", Now, Arrived);
        return profile;
    }

    private static CountryStanding Mauritius(TrustProfile profile) =>
        profile.Countries.Single(country => country.CountryCode == "MU");

    [Fact]
    public void Leaving_records_the_date_and_keeps_the_verification()
    {
        var profile = VerifiedInMauritius();

        profile.RecordDeparture("MU", Departed, Now).Should().BeTrue();

        var standing = Mauritius(profile);

        standing.DepartedOn.Should().Be(Departed);
        standing.HasDeparted.Should().BeTrue();

        // The member did live there, and did prove it. Leaving is not a reason to unprove it.
        standing.IsVerified.Should().BeTrue();
    }

    [Fact]
    public void Leaving_keeps_every_point_earned()
    {
        var profile = VerifiedInMauritius();

        profile.AddCountryPoints("MU", 400, Now);

        var before = Mauritius(profile).Points;
        var levelBefore = Mauritius(profile).Level;

        profile.RecordDeparture("MU", Departed, Now);

        Mauritius(profile).Points.Should().Be(before);
        Mauritius(profile).Level.Should().Be(levelBefore);
    }

    [Fact]
    public void Leaving_stops_the_residence_clock_at_the_departure_date()
    {
        var profile = VerifiedInMauritius();
        var asOf = new DateOnly(2026, 9, 19);

        // Still there: the clock runs to today.
        Mauritius(profile).YearsOfResidence(asOf).Should().Be(7);

        profile.RecordDeparture("MU", Departed, Now);

        // Gone: it stops where they left, which is what the guide-programme gate must read.
        Mauritius(profile).YearsOfResidence(asOf).Should().Be(6);
    }

    /// <summary>
    /// The role is derived in two places — here and in <c>AddPoints</c> — and a departure set in
    /// one of them used to be silently undone by the other.
    /// </summary>
    [Fact]
    public void A_departed_resident_is_not_called_a_resident()
    {
        var profile = VerifiedInMauritius();

        Mauritius(profile).Role.Should().Be("Verified Resident");

        profile.RecordDeparture("MU", Departed, Now);

        Mauritius(profile).Role.Should().Be("Former Resident");

        // Scoring a point after leaving must not restore the title.
        profile.AddCountryPoints("MU", 1, Now);

        Mauritius(profile).Role.Should().Be("Former Resident");
    }

    /// <summary>
    /// An earned title survives. Expertise in a country is not a statement about where somebody
    /// sleeps, and demoting a Community Expert to "Former Resident" would delete the more
    /// informative of the two facts.
    /// </summary>
    [Fact]
    public void An_earned_title_survives_the_move()
    {
        var profile = VerifiedInMauritius();

        profile.AddCountryPoints("MU", ReputationThresholds.CommunityExpert, Now);

        var earned = Mauritius(profile).Role;

        profile.RecordDeparture("MU", Departed, Now);

        Mauritius(profile).Role.Should().Be(earned);
    }

    [Fact]
    public void Coming_back_reopens_the_standing()
    {
        var profile = VerifiedInMauritius();

        profile.RecordDeparture("MU", Departed, Now);

        profile.RecordGrant(VerificationType.Country, "MU", Now, new DateOnly(2026, 1, 15));

        var standing = Mauritius(profile);

        standing.DepartedOn.Should().BeNull();
        standing.HasDeparted.Should().BeFalse();
        standing.Role.Should().Be("Verified Resident");
    }

    [Fact]
    public void A_country_the_member_never_had_standing_in_is_not_created_by_leaving_it()
    {
        var profile = VerifiedInMauritius();

        profile.RecordDeparture("GB", Departed, Now).Should().BeFalse();

        profile.Countries.Should().ContainSingle()
            .Which.CountryCode.Should().Be("MU");
    }

    /// <summary>
    /// Delivery is at-least-once, so the same departure arrives twice. The second one must be a
    /// no-op rather than a second write.
    /// </summary>
    [Fact]
    public void The_same_departure_twice_changes_nothing_the_second_time()
    {
        var profile = VerifiedInMauritius();

        profile.RecordDeparture("MU", Departed, Now).Should().BeTrue();
        profile.RecordDeparture("MU", Departed, Now).Should().BeFalse();
    }

    [Fact]
    public void Moving_on_leaves_the_first_country_standing_intact_beside_the_second()
    {
        var profile = VerifiedInMauritius();

        profile.RecordDeparture("MU", Departed, Now);
        profile.RecordGrant(VerificationType.Country, "GB", Now, new DateOnly(2025, 7, 1));

        profile.Countries.Should().HaveCount(2);

        Mauritius(profile).IsVerified.Should().BeTrue();
        Mauritius(profile).HasDeparted.Should().BeTrue();

        var britain = profile.Countries.Single(country => country.CountryCode == "GB");

        britain.IsVerified.Should().BeTrue();
        britain.HasDeparted.Should().BeFalse();
    }
}
