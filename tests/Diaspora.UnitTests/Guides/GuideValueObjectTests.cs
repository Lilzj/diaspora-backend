using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// The small types that carry the module's validation, and the reason they exist at all.
/// <para>
/// Every rule here is enforced in at least three places — creation, metadata update, version
/// restore, the search projection — and a rule enforced in three places is a rule enforced
/// inconsistently unless it lives in one type. What the tests pin is not only the accept/reject
/// boundary but the normalisation, because the normalisation is what stops two spellings of the
/// same guide becoming two entries in a search index nobody can curate.
/// </para>
/// </summary>
public class GuideValueObjectTests
{
    // ── Title ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_title_collapses_internal_whitespace_rather_than_merely_trimming_it()
    {
        var title = GuideTitle.Create("  How to  open a   bank account\t");

        title.IsSuccess.Should().BeTrue();
        title.Value.Value.Should().Be("How to open a bank account",
            "leaving the two spellings distinct produces two search entries that differ invisibly");
    }

    [Fact]
    public void Two_titles_that_normalise_the_same_are_the_same_value()
    {
        GuideTitle.Create("How to  open a bank account").Value
            .Should().Be(GuideTitle.Create("How to open a bank account").Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    public void A_title_that_cannot_mean_anything_is_refused(string? value)
    {
        var title = GuideTitle.Create(value);

        title.IsFailure.Should().BeTrue();
        title.Error.Should().Be(GuideErrors.TitleInvalid);
    }

    [Fact]
    public void A_title_is_accepted_at_the_ceiling_and_refused_one_past_it()
    {
        GuideTitle.Create(new string('t', GuideLimits.MaxTitle)).IsSuccess.Should().BeTrue();

        GuideTitle.Create(new string('t', GuideLimits.MaxTitle + 1))
            .Error.Should().Be(GuideErrors.TitleInvalid);
    }

    // ── Slug ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_slug_is_lower_cased_and_trimmed_but_never_repaired()
    {
        GuideSlug.Create("  Open-A-Bank-Account  ").Value.Value
            .Should().Be("open-a-bank-account");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    [InlineData("has space")]
    [InlineData("has_underscore")]
    [InlineData("has.dot")]
    public void A_slug_that_is_not_a_web_address_is_refused_rather_than_silently_fixed(string? value)
    {
        var slug = GuideSlug.Create(value);

        slug.IsFailure.Should().BeTrue();
        slug.Error.Should().Be(GuideErrors.SlugInvalid,
            "somebody who typed a handle wants that handle, not one we invented for them");
    }

    [Fact]
    public void A_slug_takes_a_numeric_suffix_to_resolve_a_collision()
    {
        GuideSlug.Create("open-a-bank-account").Value.WithSuffix(2).Value
            .Should().Be("open-a-bank-account-2");
    }

    [Fact]
    public void A_suffixed_slug_stays_within_the_length_the_column_holds()
    {
        var longest = GuideSlug.Create(new string('a', GuideLimits.MaxSlugLength)).Value;

        var suffixed = longest.WithSuffix(2);

        suffixed.Value.Length.Should().Be(GuideLimits.MaxSlugLength);
        suffixed.Value.Should().EndWith("-2");
    }

    [Fact]
    public void The_trusted_path_wraps_a_value_the_platform_already_validated()
    {
        GuideSlug.From("open-a-bank-account").Value.Should().Be("open-a-bank-account");

        // It is the database-read path, so an empty value there is a bug, not user input.
        var wrapEmpty = () => GuideSlug.From("   ");
        wrapEmpty.Should().Throw<ArgumentException>();
    }

    // ── Summary ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_summary_becomes_its_own_excerpt_when_the_author_supplied_none()
    {
        var summary = GuideSummary.Create("Everything you need to open an account as a newcomer.");

        summary.IsSuccess.Should().BeTrue();
        summary.Value.Excerpt.Should().Be(summary.Value.Summary,
            "a list of twenty cards must not parse twenty markdown documents to show teasers");
    }

    [Fact]
    public void A_summary_past_the_ceiling_is_refused()
    {
        var summary = GuideSummary.Create(new string('s', GuideLimits.MaxSummary + 1));

        summary.IsFailure.Should().BeTrue();
        summary.Error.Should().Be(GuideErrors.SummaryTooLong);
    }

    [Fact]
    public void A_derived_excerpt_is_truncated_rather_than_refused()
    {
        // The excerpt is derived from content the author already wrote and saved; refusing it
        // would fail a save for a field nobody typed.
        var summary = GuideSummary.Create(
            "A short summary.", new string('e', GuideLimits.MaxExcerpt + 50));

        summary.IsSuccess.Should().BeTrue();
        summary.Value.Excerpt!.Length.Should().Be(GuideLimits.MaxExcerpt);
    }

    [Fact]
    public void An_excerpt_derived_from_a_long_summary_still_respects_the_excerpt_ceiling()
    {
        // The summary's ceiling is higher than the excerpt's, so an author who writes a long
        // summary and no excerpt is the case where a derived excerpt can outgrow its own column.
        // Truncating only the supplied excerpt let that through, and the column is 300 wide:
        // a truncation error on SQL Server, silent over-length storage on SQLite.
        var summary = GuideSummary.Create(new string('s', GuideLimits.MaxSummary));

        summary.IsSuccess.Should().BeTrue();
        summary.Value.Summary!.Length.Should().Be(GuideLimits.MaxSummary,
            "the summary itself is within its own ceiling and must be kept whole");
        summary.Value.Excerpt!.Length.Should().Be(GuideLimits.MaxExcerpt,
            "a derived excerpt is bounded by the excerpt's ceiling, not the summary's");
    }

    [Fact]
    public void An_empty_summary_is_null_rather_than_an_empty_string()
    {
        var summary = GuideSummary.Create("   ");

        summary.IsSuccess.Should().BeTrue();
        summary.Value.Summary.Should().BeNull();
        summary.Value.Excerpt.Should().BeNull();
    }

    // ── Language ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("EN", "en")]
    [InlineData(" fr ", "fr")]
    [InlineData("Sw", "sw")]
    public void A_language_is_normalised_to_lower_case_iso_639_1(string input, string expected)
    {
        GuideLanguage.Create(input).Value.Code.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("e")]
    [InlineData("eng")]
    [InlineData("en-GB")]
    [InlineData("e1")]
    public void Anything_that_is_not_a_two_letter_code_is_refused(string? value)
    {
        var language = GuideLanguage.Create(value);

        language.IsFailure.Should().BeTrue();
        language.Error.Should().Be(GuideErrors.LanguageInvalid,
            "'en' versus 'en-GB' would split one English guide into regional near-duplicates " +
            "nobody maintains");
    }

    // ── Location context ────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_location_needs_a_jurisdiction_and_upper_cases_it()
    {
        var location = GuideLocationContext.Create(" gb ");

        location.IsSuccess.Should().BeTrue();
        location.Value.CountryCode.Should().Be("GB");
        location.Value.RegionName.Should().BeNull();
        location.Value.AreaPath.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("G")]
    [InlineData("GBR")]
    [InlineData("G1")]
    public void A_guide_without_a_jurisdiction_is_refused(string? value)
    {
        var location = GuideLocationContext.Create(value);

        location.IsFailure.Should().BeTrue();
        location.Error.Should().Be(GuideErrors.CountryInvalid,
            "procedural knowledge with no jurisdiction is a blog post, not a guide");
    }

    [Fact]
    public void The_area_path_is_kept_as_a_plain_prefix_matchable_string()
    {
        var areaId = Guid.NewGuid();

        var location = GuideLocationContext.Create(
            "GB", "England", "London", areaId, "/gb/england/london");

        location.Value.AreaId.Should().Be(areaId);
        location.Value.AreaPath.Should().Be("/gb/england/london",
            "the column exists so LIKE 'prefix%' translates, which a converted value would not");
    }

    [Fact]
    public void Location_names_are_truncated_to_what_the_columns_hold()
    {
        var location = GuideLocationContext.Create(
            "GB",
            new string('r', GuideLimits.MaxLocationName + 20),
            cityName: null,
            areaId: null,
            areaPath: new string('p', GuideLimits.MaxAreaPath + 20));

        location.Value.RegionName!.Length.Should().Be(GuideLimits.MaxLocationName);
        location.Value.AreaPath!.Length.Should().Be(GuideLimits.MaxAreaPath);
    }

    // ── SEO ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_empty_seo_block_is_always_present_so_the_read_path_never_branches()
    {
        GuideSeoMetadata.Empty.MetaTitle.Should().BeNull();
        GuideSeoMetadata.Empty.CanonicalUrl.Should().BeNull();
    }

    [Fact]
    public void An_absolute_https_canonical_is_accepted()
    {
        var seo = GuideSeoMetadata.Create(
            "Open a UK bank account",
            "What a newcomer needs to open an account.",
            "https://diaspora.example/guides/open-a-uk-bank-account",
            "banking,uk,newcomer");

        seo.IsSuccess.Should().BeTrue();
        seo.Value.CanonicalUrl.Should().Be("https://diaspora.example/guides/open-a-uk-bank-account");
    }

    [Theory]
    [InlineData("http://diaspora.example/guides/one")]
    [InlineData("/guides/one")]
    [InlineData("diaspora.example/guides/one")]
    [InlineData("ftp://diaspora.example/guides/one")]
    public void A_canonical_that_is_not_absolute_https_is_refused(string canonical)
    {
        var seo = GuideSeoMetadata.Create(null, null, canonical, null);

        seo.IsFailure.Should().BeTrue();
        seo.Error.Should().Be(GuideErrors.SeoInvalid,
            "a wrong canonical tells a crawler the guide's authoritative home is somewhere it is not");
    }

    [Fact]
    public void Seo_text_past_what_a_search_engine_renders_is_refused()
    {
        GuideSeoMetadata.Create(new string('t', GuideLimits.MaxSeoTitle + 1), null, null, null)
            .Error.Should().Be(GuideErrors.SeoInvalid);

        GuideSeoMetadata.Create(null, new string('d', GuideLimits.MaxSeoDescription + 1), null, null)
            .Error.Should().Be(GuideErrors.SeoInvalid);

        GuideSeoMetadata.Create(null, null, null, new string('k', GuideLimits.MaxSeoKeywords + 1))
            .Error.Should().Be(GuideErrors.SeoInvalid);
    }

    // ── Reading profile ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_new_guide_starts_at_the_default_reading_profile()
    {
        GuideReadingProfile.Default.Difficulty.Should().Be(GuideDifficulty.Beginner);
        GuideReadingProfile.Default.ReadingTimeMinutes.Should().Be(1);
        GuideReadingProfile.Default.EstimatedCompletionMinutes.Should().BeNull();
    }

    [Fact]
    public void A_reading_time_below_one_minute_is_refused()
    {
        GuideReadingProfile.Create(GuideDifficulty.Beginner, 0)
            .Error.Should().Be(GuideErrors.ReadingProfileInvalid);
    }

    [Fact]
    public void A_completion_estimate_is_optional_but_never_zero()
    {
        GuideReadingProfile.Create(GuideDifficulty.Advanced, 5, null)
            .IsSuccess.Should().BeTrue("only the author knows how long a visa process takes");

        GuideReadingProfile.Create(GuideDifficulty.Advanced, 5, 0)
            .Error.Should().Be(GuideErrors.ReadingProfileInvalid);
    }

    [Fact]
    public void Recomputing_the_reading_time_keeps_everything_the_author_chose()
    {
        var profile = GuideReadingProfile.Create(GuideDifficulty.Advanced, 1, 20_160).Value;

        var recomputed = profile.WithReadingTime(7);

        recomputed.ReadingTimeMinutes.Should().Be(7);
        recomputed.Difficulty.Should().Be(GuideDifficulty.Advanced);
        recomputed.EstimatedCompletionMinutes.Should().Be(20_160,
            "publication recomputes reading time and nothing else");

        profile.WithReadingTime(0).ReadingTimeMinutes.Should().Be(1);
    }

    // ── Engagement ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_new_guide_starts_at_every_counter_zero()
    {
        var engagement = GuideEngagement.None;

        engagement.Views.Should().Be(0);
        engagement.UniqueViews.Should().Be(0);
        engagement.HelpfulCount.Should().Be(0);
        engagement.NotHelpfulCount.Should().Be(0);
        engagement.BookmarkCount.Should().Be(0);
        engagement.ShareCount.Should().Be(0);
        engagement.CommentCount.Should().Be(0);
    }

    [Fact]
    public void A_repeat_view_counts_once_towards_unique_readers()
    {
        var engagement = GuideEngagement.None.WithView(unique: true).WithView(unique: false);

        engagement.Views.Should().Be(2);
        engagement.UniqueViews.Should().Be(1);
    }

    [Fact]
    public void A_decrement_that_arrives_twice_cannot_drive_a_public_number_negative()
    {
        // At-least-once delivery makes the double decrement the ordinary case, and a card
        // showing "-1 bookmarks" is worse than one that is briefly one off.
        var engagement = GuideEngagement.None
            .WithBookmarkDelta(1)
            .WithBookmarkDelta(-1)
            .WithBookmarkDelta(-1);

        engagement.BookmarkCount.Should().Be(0);

        GuideEngagement.None.WithVoteDelta(-5, -5).Should().Be(GuideEngagement.None);
        GuideEngagement.None.WithCommentCount(-3).CommentCount.Should().Be(0);
    }

    [Fact]
    public void The_comment_count_is_replaced_because_community_owns_it()
    {
        var engagement = GuideEngagement.None.WithCommentCount(12).WithCommentCount(9);

        engagement.CommentCount.Should().Be(9,
            "Community recounts and tells us the answer; we never delta our way to it");
    }

    [Fact]
    public void Every_counter_change_leaves_the_others_alone()
    {
        var engagement = GuideEngagement.None
            .WithView(unique: true)
            .WithVoteDelta(3, 1)
            .WithBookmarkDelta(2)
            .WithCommentCount(4)
            .WithShare();

        engagement.Views.Should().Be(1);
        engagement.UniqueViews.Should().Be(1);
        engagement.HelpfulCount.Should().Be(3);
        engagement.NotHelpfulCount.Should().Be(1);
        engagement.BookmarkCount.Should().Be(2);
        engagement.CommentCount.Should().Be(4);
        engagement.ShareCount.Should().Be(1);
    }
}
