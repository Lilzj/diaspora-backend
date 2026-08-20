using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Jobs;

/// <summary>
/// The value objects that carry this platform's cross-border rules. Most of these tests exist
/// because getting the rule wrong would be invisible until somebody wasted a day on it.
/// </summary>
public class JobValueObjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    // ── Salary ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Undisclosed is a first-class state, not a null. It lets a client render "salary not
    /// disclosed" rather than nothing.
    /// </summary>
    [Fact]
    public void No_figure_produces_an_undisclosed_salary_rather_than_a_failure()
    {
        var salary = SalaryRange.Create(null, null, null, null);

        salary.IsSuccess.Should().BeTrue();
        salary.Value.IsDisclosed.Should().BeFalse();
        salary.Value.ComparableAmount.Should().BeNull();
    }

    /// <summary>
    /// "50,000" is unreadable across borders, and this platform's entire audience reads across
    /// borders.
    /// </summary>
    [Fact]
    public void A_figure_without_a_period_is_refused()
    {
        SalaryRange.Create(50_000m, null, "EUR", period: null)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_figure_without_a_currency_is_refused()
    {
        SalaryRange.Create(50_000m, null, currencyCode: null, SalaryPeriod.Yearly)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void An_inverted_band_is_refused()
    {
        SalaryRange.Create(80_000m, 40_000m, "EUR", SalaryPeriod.Yearly)
            .IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("eur", "EUR")]
    [InlineData(" gbp ", "GBP")]
    public void A_currency_is_normalised_to_upper_case(string input, string expected) =>
        SalaryRange.Create(1m, null, input, SalaryPeriod.Yearly)
            .Value.CurrencyCode.Should().Be(expected);

    [Theory]
    [InlineData("EU")]
    [InlineData("EUROS")]
    [InlineData("E1R")]
    public void A_malformed_currency_is_refused(string currency) =>
        SalaryRange.Create(1m, null, currency, SalaryPeriod.Yearly)
            .IsFailure.Should().BeTrue();

    /// <summary>The sort orders by the top of the band, or the bottom when there is no band.</summary>
    [Fact]
    public void The_comparable_amount_is_the_top_of_the_band()
    {
        SalaryRange.Create(40_000m, 60_000m, "EUR", SalaryPeriod.Yearly)
            .Value.ComparableAmount.Should().Be(60_000m);

        SalaryRange.Create(40_000m, null, "EUR", SalaryPeriod.Yearly)
            .Value.ComparableAmount.Should().Be(40_000m);
    }

    // ── Work arrangement ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A contradiction the employer did not mean. Normalised rather than refused, because
    /// clients send the whole form back and the field is simply stale.
    /// </summary>
    [Fact]
    public void Relocation_assistance_is_dropped_for_a_fully_remote_role()
    {
        var arrangement = WorkArrangement.Create(
            WorkplaceType.Remote,
            offersVisaSponsorship: true,
            offersRelocationAssistance: true);

        arrangement.Value.OffersRelocationAssistance.Should().BeFalse();
        arrangement.Value.OffersVisaSponsorship.Should().BeTrue(
            "a remote role can still need a right to work somewhere");
    }

    [Fact]
    public void On_site_days_only_mean_something_for_a_hybrid_arrangement()
    {
        WorkArrangement.Create(WorkplaceType.Remote, onSiteDaysPerWeek: 3)
            .Value.OnSiteDaysPerWeek.Should().BeNull();

        WorkArrangement.Create(WorkplaceType.Hybrid, onSiteDaysPerWeek: 3)
            .Value.OnSiteDaysPerWeek.Should().Be(3);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void An_impossible_number_of_office_days_is_refused(int days) =>
        WorkArrangement.Create(WorkplaceType.Hybrid, onSiteDaysPerWeek: days)
            .IsFailure.Should().BeTrue();

    // ── Location ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// "Remote" is never actually borderless: it means remote within a jurisdiction whose
    /// employment law and tax the employer has accepted.
    /// </summary>
    [Fact]
    public void A_country_is_required_even_for_a_remote_posting()
    {
        JobLocationContext.Create(countryCode: null).IsFailure.Should().BeTrue();
        JobLocationContext.Create("  ").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void The_display_name_is_the_most_specific_place_named()
    {
        JobLocationContext.Create("DE", "Berlin", "Kreuzberg").Value
            .DisplayName.Should().Be("Kreuzberg");

        JobLocationContext.Create("DE", "Bavaria").Value.DisplayName.Should().Be("Bavaria");

        JobLocationContext.Create("DE").Value.DisplayName.Should().Be("DE");
    }

    // ── Skill keys: the match that has to work on both sides ────────────────────────────────

    /// <summary>
    /// If a posting asking for "React.js" and a candidate claiming "reactjs" do not land on the
    /// same key, neither will ever find the other.
    /// </summary>
    [Theory]
    [InlineData("React.js", "react-js")]
    [InlineData("  REACT JS  ", "react-js")]
    [InlineData("react-js", "react-js")]
    public void Skill_keys_normalise_to_the_same_string(string input, string expected) =>
        JobSkillRequirement.NormalizeKey(input).Should().Be(expected);

    /// <summary>
    /// '+' and '#' are load-bearing in skill names. Folding them away makes "c#" and "c" the
    /// same skill.
    /// </summary>
    [Theory]
    [InlineData("C#", "c#")]
    [InlineData("C++", "c++")]
    public void Skill_keys_keep_the_characters_that_distinguish_languages(
        string input, string expected) =>
        JobSkillRequirement.NormalizeKey(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void An_unusable_skill_name_normalises_to_nothing(string? input) =>
        JobSkillRequirement.NormalizeKey(input).Should().BeNull();

    // ── Slugs ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A slug that silently lost the letters of a language is not a neutral default on a
    /// platform whose members mostly do not write in ASCII.
    /// </summary>
    [Fact]
    public void Diacritics_are_folded_rather_than_dropped()
    {
        JobSlug.FromParts("Bäcker gesucht").Value.Value.Should().Be("backer-gesucht");
        JobSlug.FromParts("Café Manager").Value.Value.Should().Be("cafe-manager");
    }

    [Fact]
    public void A_derived_slug_joins_the_parts_that_distinguish_it()
    {
        JobSlug.FromParts("Nurse", "Charité Berlin", "Berlin").Value.Value
            .Should().Be("nurse-charite-berlin-berlin");
    }

    [Theory]
    [InlineData("Not A Slug")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    public void An_explicit_slug_is_validated_rather_than_repaired(string candidate) =>
        JobSlug.Create(candidate).IsFailure.Should().BeTrue();

    // ── Offer terms ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A candidate being asked to accept a job is entitled to the number in writing, and to know
    /// how long they have.
    /// </summary>
    [Fact]
    public void An_offer_deadline_in_the_past_is_refused() =>
        OfferTerms.Create(
            50_000m, "EUR", SalaryPeriod.Yearly, Now.AddDays(-1), Now)
            .IsFailure.Should().BeTrue();

    [Fact]
    public void An_offer_start_date_in_the_past_is_refused() =>
        OfferTerms.Create(
            50_000m,
            "EUR",
            SalaryPeriod.Yearly,
            Now.AddDays(7),
            Now,
            startDate: DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-1))
            .IsFailure.Should().BeTrue();

    [Fact]
    public void Extending_a_deadline_produces_new_terms_with_the_same_money()
    {
        var terms = OfferTerms.Create(
            50_000m, "EUR", SalaryPeriod.Yearly, Now.AddDays(3), Now).Value;

        var extended = terms.WithDeadline(Now.AddDays(10), Now);

        extended.IsSuccess.Should().BeTrue();
        extended.Value.ExpiresAt.Should().Be(Now.AddDays(10));
        extended.Value.SalaryAmount.Should().Be(50_000m, "extending time changes no money");
    }

    // ── Interview schedule: the mode decides what is required ───────────────────────────────

    [Fact]
    public void An_on_site_interview_without_an_address_is_refused() =>
        InterviewSchedule.Create(Now.AddDays(2), 60, InterviewMode.OnSite, Now)
            .IsFailure.Should().BeTrue(
                "a cross-border candidate cannot tell whether they need a visa appointment");

    [Fact]
    public void A_video_interview_without_a_joining_link_is_refused() =>
        InterviewSchedule.Create(Now.AddDays(2), 60, InterviewMode.Video, Now)
            .IsFailure.Should().BeTrue();

    [Fact]
    public void An_unattended_assessment_needs_neither() =>
        InterviewSchedule.Create(Now.AddDays(2), 60, InterviewMode.Assessment, Now)
            .IsSuccess.Should().BeTrue();

    [Fact]
    public void An_interview_in_the_past_is_refused() =>
        InterviewSchedule.Create(
            Now.AddHours(-1), 60, InterviewMode.Assessment, Now)
            .IsFailure.Should().BeTrue();

    [Fact]
    public void A_joining_link_must_be_absolute_http() =>
        InterviewSchedule.Create(
            Now.AddDays(2), 60, InterviewMode.Video, Now,
            joiningUrl: "javascript:alert(1)")
            .IsFailure.Should().BeTrue("a joining link is rendered as a link");

    // ── Employer responsiveness: the rates that describe a named organisation ────────────────

    /// <summary>
    /// A rate computed from one event is noise presented as a fact about a named organisation,
    /// and the platform generated it.
    /// </summary>
    [Fact]
    public void A_rate_is_withheld_below_the_sample_threshold()
    {
        var metrics = EmployerResponsiveness.None.WithIgnored().WithIgnored();

        metrics.GhostingRate.Should().BeNull(
            "two ignored applications is not a pattern worth publishing");
    }

    [Fact]
    public void A_rate_is_published_once_there_is_enough_to_say()
    {
        var metrics = EmployerResponsiveness.None;

        for (var index = 0; index < 5; index++)
        {
            metrics = metrics.WithIgnored();
        }

        metrics.GhostingRate.Should().Be(1d);
    }

    /// <summary>
    /// Null is an unknown figure, which is not the same as a fast one and must not render as
    /// zero.
    /// </summary>
    [Fact]
    public void An_employer_that_has_answered_nothing_has_no_response_time() =>
        EmployerResponsiveness.None.AverageResponseHours.Should().BeNull();

    [Fact]
    public void The_mean_response_time_is_the_running_average()
    {
        var metrics = EmployerResponsiveness.None
            .WithResponse(TimeSpan.FromHours(2))
            .WithResponse(TimeSpan.FromHours(4));

        metrics.AverageResponseHours.Should().BeApproximately(3d, 0.01);
    }

    /// <summary>A clock skew or a backfill must not corrupt a public reputation figure.</summary>
    [Fact]
    public void A_negative_interval_is_dropped()
    {
        var metrics = EmployerResponsiveness.None.WithResponse(TimeSpan.FromHours(-5));

        metrics.RespondedCount.Should().Be(0);
        metrics.AverageResponseHours.Should().BeNull();
    }

    // ── Engagement counters ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A decrement arriving twice through at-least-once delivery must not produce a negative
    /// applicant count on a card.
    /// </summary>
    [Fact]
    public void Counters_never_go_negative()
    {
        var engagement = JobEngagement.None
            .WithApplicationDelta(-1)
            .WithSaveDelta(-5);

        engagement.ApplicationCount.Should().Be(0);
        engagement.SaveCount.Should().Be(0);
    }

    [Fact]
    public void The_application_rate_is_zero_when_nobody_has_looked() =>
        JobEngagement.None.ApplicationRate.Should().Be(0d);

    // ── Employment period ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// "Still there" and "ended on this date" cannot both be true. The end date is the more
    /// specific claim, so it wins.
    /// </summary>
    [Fact]
    public void An_end_date_beats_a_still_current_flag()
    {
        var period = EmploymentPeriod.Create(
            new DateOnly(2020, 1, 1), new DateOnly(2023, 6, 30), isCurrent: true);

        period.Value.IsCurrent.Should().BeFalse();
        period.Value.End.Should().Be(new DateOnly(2023, 6, 30));
    }

    [Fact]
    public void An_inverted_period_is_refused() =>
        EmploymentPeriod.Create(new DateOnly(2023, 1, 1), new DateOnly(2020, 1, 1))
            .IsFailure.Should().BeTrue();

    [Fact]
    public void Months_elapsed_counts_to_today_while_still_current()
    {
        var period = EmploymentPeriod.Create(
            new DateOnly(2025, 7, 30), end: null, isCurrent: true).Value;

        period.MonthsElapsed(new DateOnly(2026, 7, 30)).Should().Be(12);
    }
}
