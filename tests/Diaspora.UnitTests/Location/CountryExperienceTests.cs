using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.Modules.Location.Domain.Residency;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Location;

/// <summary>
/// Residence history: the claim Location owns, and the proof it only ever projects.
/// </summary>
public sealed class CountryExperienceTests
{
    private static readonly DateOnly Today = new(2026, 7, 27);
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static CountryExperience Declare(int arrivalYear, int? departureYear = null)
    {
        var period = ResidencePeriod.TryCreate(
            new DateOnly(arrivalYear, 1, 1),
            departureYear is null ? null : new DateOnly(departureYear.Value, 1, 1),
            Today).Value;

        return CountryExperience.Declare(
            UserId.New(), CountryCode.Create("GB"), period, "Diaspora Member").Value;
    }

    // ── Periods ──────────────────────────────────────────────────────────

    [Fact]
    public void An_arrival_in_the_future_is_rejected()
    {
        ResidencePeriod
            .TryCreate(new DateOnly(2030, 1, 1), null, Today)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_departure_before_the_arrival_is_rejected()
    {
        ResidencePeriod
            .TryCreate(new DateOnly(2020, 1, 1), new DateOnly(2019, 1, 1), Today)
            .IsFailure.Should().BeTrue();
    }

    /// <summary>A mistyped year is far more likely than a 150-year residence.</summary>
    [Fact]
    public void An_implausible_arrival_year_is_rejected()
    {
        ResidencePeriod
            .TryCreate(new DateOnly(1850, 1, 1), null, Today)
            .IsFailure.Should().BeTrue();
    }

    /// <summary>An open period is "Present" in the timeline, not a far-future sentinel date.</summary>
    [Fact]
    public void An_open_period_reports_itself_as_a_current_residence()
    {
        var experience = Declare(2019);

        experience.IsCurrentResidence.Should().BeTrue();
        experience.Period.DepartedOn.Should().BeNull();
    }

    /// <summary>
    /// Two overlapping periods in one country are not a richer history, they are a data-entry
    /// error that doubles every derived years-of-residence figure.
    /// </summary>
    [Fact]
    public void Overlapping_periods_are_detectable()
    {
        var first = ResidencePeriod.TryCreate(
            new DateOnly(2015, 1, 1), new DateOnly(2020, 1, 1), Today).Value;

        var overlapping = ResidencePeriod.TryCreate(
            new DateOnly(2018, 1, 1), new DateOnly(2022, 1, 1), Today).Value;

        var disjoint = ResidencePeriod.TryCreate(
            new DateOnly(2021, 1, 1), new DateOnly(2022, 1, 1), Today).Value;

        first.Overlaps(overlapping).Should().BeTrue();
        first.Overlaps(disjoint).Should().BeFalse();
    }

    // ── Derived experience ───────────────────────────────────────────────

    /// <summary>
    /// Derived from the calendar rather than declared, so a member's standing grows on its own
    /// and cannot be self-inflated.
    /// </summary>
    [Theory]
    [InlineData(2026, ExperienceLevel.Settling)]
    [InlineData(2025, ExperienceLevel.Settling)]
    [InlineData(2023, ExperienceLevel.Established)]
    [InlineData(2019, ExperienceLevel.Seasoned)]
    [InlineData(2005, ExperienceLevel.Native)]
    public void Experience_level_is_derived_from_time_in_country(
        int arrivalYear, ExperienceLevel expected)
    {
        Declare(arrivalYear).LevelAsAt(Today).Should().Be(expected);
    }

    /// <summary>
    /// The first band is measured in months, not years — six months in is still finding the
    /// supermarket, and a member who arrived in January of the current year is past it.
    /// </summary>
    [Fact]
    public void Someone_who_arrived_last_month_is_a_newcomer()
    {
        var period = ResidencePeriod.TryCreate(
            Today.AddMonths(-1), null, Today).Value;

        period.ExperienceAsAt(Today).Should().Be(ExperienceLevel.Newcomer);
    }

    [Fact]
    public void Years_are_counted_to_the_departure_when_the_period_is_closed()
    {
        Declare(2015, 2020).YearsAsAt(Today).Should().Be(5);
    }

    // ── Expertise ────────────────────────────────────────────────────────

    /// <summary>
    /// The wizard posts the whole array on every save, so claiming the same area twice has to
    /// be a no-op rather than a growing list.
    /// </summary>
    [Fact]
    public void Claiming_the_same_expertise_twice_does_not_duplicate_it()
    {
        var experience = Declare(2019);

        experience.ClaimExpertise("Housing", FamiliarityLevel.Fluent, Now);
        experience.ClaimExpertise("housing", FamiliarityLevel.Native, Now);

        experience.Expertise.Should().HaveCount(1);
        experience.Expertise[0].Confidence.Should().Be(FamiliarityLevel.Native);
    }

    [Fact]
    public void Replacing_expertise_removes_what_is_no_longer_claimed()
    {
        var experience = Declare(2019);

        experience.ReplaceExpertise(["Housing", "Jobs", "Visas"], Now);
        experience.ReplaceExpertise(["Housing", "Schools"], Now);

        experience.Expertise.Select(claim => claim.Area)
            .Should().BeEquivalentTo("Housing", "Schools");
    }

    /// <summary>A profile claiming thirty areas of expertise claims none.</summary>
    [Fact]
    public void Expertise_is_capped()
    {
        var experience = Declare(2019);

        var tooMany = Enumerable
            .Range(1, CountryExperience.MaxExpertiseAreas + 1)
            .Select(index => $"Area {index}")
            .ToList();

        experience.ReplaceExpertise(tooMany, Now).IsFailure.Should().BeTrue();
    }

    // ── The boundary with Trust ──────────────────────────────────────────

    /// <summary>
    /// The load-bearing assertion of the whole module. If a verification flag ever appears on
    /// this aggregate, two modules have authority over one fact and this one will be the stale
    /// one.
    /// </summary>
    [Fact]
    public void The_experience_aggregate_holds_no_verification_state()
    {
        var properties = typeof(CountryExperience)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        properties.Should().NotContain("IsVerified");
        properties.Should().NotContain("VerificationState");
        properties.Should().NotContain("VerifiedAt");
    }

    /// <summary>
    /// The projection is the only thing that records a verification, and it only ever records
    /// what Trust published.
    /// </summary>
    [Fact]
    public void A_residency_projection_records_what_trust_decided()
    {
        var residency = VerifiedResidency.For(UserId.New(), CountryCode.Create("GB"));

        residency.IsVerifiedAt(Now).Should().BeFalse();

        residency.Apply(
            ResidencyVerificationState.Verified,
            Guid.NewGuid(),
            decidedAt: Now.AddDays(-1),
            observedAt: Now);

        residency.IsVerifiedAt(Now).Should().BeTrue();
    }

    /// <summary>
    /// Delivery is at-least-once and unordered. Without the guard, a retried approval can
    /// resurrect a revocation.
    /// </summary>
    [Fact]
    public void An_out_of_order_event_is_ignored()
    {
        var residency = VerifiedResidency.For(UserId.New(), CountryCode.Create("GB"));
        var verificationId = Guid.NewGuid();

        residency.Apply(
            ResidencyVerificationState.Revoked, verificationId,
            decidedAt: Now, observedAt: Now, reason: "fraud");

        var applied = residency.Apply(
            ResidencyVerificationState.Verified, verificationId,
            decidedAt: Now.AddDays(-1), observedAt: Now);

        applied.Should().BeFalse();
        residency.State.Should().Be(ResidencyVerificationState.Revoked);
    }

    /// <summary>
    /// A lapsed grant stops counting the moment it lapses, not the next time a sweep runs.
    /// </summary>
    [Fact]
    public void An_expired_grant_stops_counting_as_verified()
    {
        var residency = VerifiedResidency.For(UserId.New(), CountryCode.Create("GB"));

        residency.Apply(
            ResidencyVerificationState.Verified,
            Guid.NewGuid(),
            decidedAt: Now.AddYears(-2),
            observedAt: Now,
            expiresAt: Now.AddDays(-1));

        residency.State.Should().Be(ResidencyVerificationState.Verified);
        residency.IsVerifiedAt(Now).Should().BeFalse();
    }

    /// <summary>A redelivery of the same event is harmless and must not be skipped.</summary>
    [Fact]
    public void A_redelivery_at_the_same_instant_is_applied()
    {
        var residency = VerifiedResidency.For(UserId.New(), CountryCode.Create("GB"));
        var verificationId = Guid.NewGuid();

        residency.Apply(
            ResidencyVerificationState.Verified, verificationId,
            decidedAt: Now, observedAt: Now);

        var second = residency.Apply(
            ResidencyVerificationState.Verified, verificationId,
            decidedAt: Now, observedAt: Now.AddSeconds(1));

        // Applied, but reported as no change, so the caller skips a pointless save.
        second.Should().BeFalse();
        residency.State.Should().Be(ResidencyVerificationState.Verified);
    }
}
