using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.UnitTests.Knowledge;

/// <summary>
/// One country's knowledge directory — the aggregate that makes the platform country-first.
/// <para>
/// <b>Two rules carry most of the weight and both are asserted several ways below.</b> Position
/// comes from the order of the submitted list and never from a number the caller supplies, because a
/// caller-supplied position is a caller-supplied bug — two rows claiming three, a gap at seven after
/// a delete, an ordering that renders differently from the list somebody dragged. And featuring is
/// country-scoped, because the same guide is legitimately the first thing to read about Mauritius
/// and irrelevant to Canada, which one global boolean cannot express.
/// </para>
/// </summary>
public class CountryKnowledgeDirectoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly UserId Curator = UserId.From(Guid.NewGuid());

    // ── Creation ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_created_directory_is_unpublished_and_carries_its_normalised_country()
    {
        var directory = Create("mu", "Mauritius");

        directory.IsSuccess.Should().BeTrue();
        directory.Value.CountryCode.Should().Be("MU");
        directory.Value.IsPublished.Should().BeFalse("publishing is a separate, deliberate act");
        directory.Value.PublishedAt.Should().BeNull();
        directory.Value.Entries.Should().BeEmpty();
    }

    [Fact]
    public void A_directory_for_a_country_nobody_has_written_copy_for_is_named_after_its_code()
    {
        // A template contributor can open a country before an editor writes the header, and a
        // nameless directory would be worse than one named "MU".
        Create("MU", null).Value.DisplayName.Should().Be("MU");
    }

    [Theory]
    [InlineData("M")]
    [InlineData("MUS")]
    [InlineData(null)]
    public void A_country_code_that_is_not_two_letters_is_refused(string? code) =>
        Create(code, "Anywhere").Error.Should().Be(KnowledgeErrors.CountryInvalid);

    [Fact]
    public void An_unusable_declared_language_fails_the_creation_rather_than_being_dropped()
    {
        var directory = CountryKnowledgeDirectory.Create(
            CountryKnowledgeDirectoryId.New(), "MU", "Mauritius", Now,
            languages: ["en", "nope!"]);

        directory.Error.Should().Be(
            KnowledgeErrors.LocaleInvalid,
            "silently dropping a curator's whole language list over one typo helps nobody");
    }

    // ── Arrangement ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The rule the whole curation surface depends on.</b> Nothing about position is negotiable
    /// with the caller: the aggregate renumbers contiguously from zero on every save, so the stored
    /// order is always exactly what somebody arranged.
    /// </summary>
    [Fact]
    public void Position_comes_from_the_list_order_and_is_contiguous_from_zero()
    {
        var directory = Create("MU", "Mauritius").Value;

        directory.Arrange(
            [
                ("getting-started", null, false),
                ("immigration", null, false),
                ("housing", null, false)
            ],
            Curator);

        directory.Entries.Select(entry => entry.Position).Should().BeEquivalentTo([0, 1, 2]);

        directory.Entries.Single(entry => entry.KnowledgeCategoryKey == "housing")
            .Position.Should().Be(2);
    }

    [Fact]
    public void Rearranging_renumbers_rather_than_leaving_a_gap()
    {
        var directory = Arranged("a", "b", "c");

        directory.Arrange([("c", null, false), ("a", null, false)], Curator);

        directory.Entries.Should().HaveCount(2);
        directory.Entries.Select(entry => entry.Position).Should().BeEquivalentTo([0, 1]);
        directory.Entries.Single(entry => entry.Position == 0).KnowledgeCategoryKey.Should().Be("c");
    }

    /// <summary>
    /// Rejected rather than collapsed, unlike a duplicated translation: nobody drags the same row
    /// into a list twice, so a repeat is unambiguous evidence of a broken client.
    /// </summary>
    [Fact]
    public void A_category_placed_twice_is_refused()
    {
        var directory = Create("MU", "Mauritius").Value;

        var result = directory.Arrange(
            [("housing", null, false), ("HOUSING", null, false)], Curator);

        result.Error.Should().Be(KnowledgeErrors.EntryDuplicate);
    }

    [Fact]
    public void More_rows_than_a_country_page_can_list_are_refused()
    {
        var directory = Create("MU", "Mauritius").Value;

        var many = Enumerable.Range(0, 100)
            .Select(index => ((string?)$"row-{index}", (string?)null, false))
            .ToList();

        directory.Arrange(many, Curator).Error.Should().Be(KnowledgeErrors.EntryLimit);
    }

    /// <summary>
    /// A hidden row keeps its place. A curator preparing a launch arranges twenty rows over a
    /// fortnight and needs the eight with no guides yet to stay where they put them.
    /// </summary>
    [Fact]
    public void A_hidden_row_is_arranged_but_not_shown()
    {
        var directory = Create("MU", "Mauritius").Value;

        directory.Arrange(
            [("immigration", null, false), ("retirement", null, true), ("housing", null, false)],
            Curator);

        directory.Entries.Should().HaveCount(3);
        directory.VisibleEntries.Should().HaveCount(2);
        directory.VisibleEntries.Select(entry => entry.KnowledgeCategoryKey)
            .Should().BeEquivalentTo(["immigration", "housing"]);

        directory.Lists("retirement").Should().BeTrue(
            "a hidden row is still listed — it is arranged, and archiving its category must be refused");
    }

    [Fact]
    public void Arranging_raises_the_change_event_a_cache_invalidates_from()
    {
        var directory = Create("MU", "Mauritius").Value;
        directory.ClearDomainEvents();

        directory.Arrange([("immigration", null, false)], Curator);

        var raised = directory.DomainEvents
            .OfType<CountryDirectoryChangedDomainEvent>()
            .Single();

        raised.Change.Should().Be(KnowledgeChangeKinds.Arranged);
        raised.CountryCode.Should().Be("MU");
        raised.CategoryCount.Should().Be(1);
    }

    // ── Curated featuring ───────────────────────────────────────────────────────────────────

    [Fact]
    public void A_featured_guide_lands_on_the_country_shelf_by_default()
    {
        var directory = Create("MU", "Mauritius").Value;
        var guide = GuideId.New();

        Feature(directory, guide).IsSuccess.Should().BeTrue();

        directory.FeaturedFor(null).Should().BeEquivalentTo([guide.Value]);
        directory.FeaturedFor("housing").Should().BeEmpty();
    }

    /// <summary>
    /// The country shelf and each category shelf are separate lists. A guide can sit on both, and
    /// removing it from one leaves the other alone.
    /// </summary>
    [Fact]
    public void The_country_shelf_and_a_category_shelf_are_different_shelves()
    {
        var directory = Arranged("housing");
        var guide = GuideId.New();

        Feature(directory, guide).IsSuccess.Should().BeTrue();
        Feature(directory, guide, "housing").IsSuccess.Should().BeTrue();

        directory.FeaturedFor(null).Should().ContainSingle();
        directory.FeaturedFor("housing").Should().ContainSingle();

        directory.UnfeatureGuide(guide, "housing", Curator).IsSuccess.Should().BeTrue();

        directory.FeaturedFor(null).Should().ContainSingle("the country shelf was not touched");
        directory.FeaturedFor("housing").Should().BeEmpty();
    }

    [Fact]
    public void Featuring_the_same_guide_on_the_same_shelf_twice_is_refused()
    {
        var directory = Create("MU", "Mauritius").Value;
        var guide = GuideId.New();

        Feature(directory, guide);

        Feature(directory, guide).Error.Should().Be(KnowledgeErrors.AlreadyFeatured);
    }

    [Fact]
    public void A_supplied_position_inserts_and_renumbers_the_shelf()
    {
        var directory = Create("MU", "Mauritius").Value;

        var first = GuideId.New();
        var second = GuideId.New();
        var inserted = GuideId.New();

        Feature(directory, first);
        Feature(directory, second);
        Feature(directory, inserted, position: 0);

        directory.FeaturedFor(null).Should().BeEquivalentTo(
            new[] { inserted.Value, first.Value, second.Value },
            options => options.WithStrictOrdering());

        directory.FeaturedGuides.Select(featured => featured.Position)
            .Should().BeEquivalentTo([0, 1, 2]);
    }

    [Fact]
    public void Unfeaturing_something_that_is_not_on_the_shelf_is_refused()
    {
        var directory = Create("MU", "Mauritius").Value;

        directory.UnfeatureGuide(GuideId.New(), null, Curator)
            .Error.Should().Be(KnowledgeErrors.NotFeatured);
    }

    [Fact]
    public void Featuring_raises_a_country_scoped_event_carrying_the_placement()
    {
        var directory = Arranged("housing");
        directory.ClearDomainEvents();

        var guide = GuideId.New();

        Feature(directory, guide, "housing", note: "Read this before you sign anything.");

        var raised = directory.DomainEvents
            .OfType<CountryGuideFeatureChangedDomainEvent>()
            .Single();

        raised.Featured.Should().BeTrue();
        raised.CountryCode.Should().Be("MU");
        raised.KnowledgeCategoryKey.Should().Be("housing");
        raised.Note.Should().Be("Read this before you sign anything.");
        raised.ChangedBy.Should().Be(Curator);
    }

    [Fact]
    public void More_featured_guides_than_a_shelf_can_hold_are_refused()
    {
        var directory = Create("MU", "Mauritius").Value;

        for (var index = 0; index < 50; index++)
        {
            Feature(directory, GuideId.New()).IsSuccess.Should().BeTrue();
        }

        Feature(directory, GuideId.New()).Error.Should().Be(KnowledgeErrors.FeaturedLimit);
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Publishing_makes_the_country_visible_and_stamps_the_instant()
    {
        var directory = Create("MU", "Mauritius").Value;

        directory.Publish(Now, Curator).IsSuccess.Should().BeTrue();

        directory.IsPublished.Should().BeTrue();
        directory.PublishedAt.Should().Be(Now);

        directory.Publish(Now, Curator).Error.Should()
            .Be(KnowledgeErrors.DirectoryAlreadyPublished);
    }

    /// <summary>
    /// The launch instant is a fact about the platform's history, not about the current state.
    /// Clearing it would make a republished country look brand new to every consumer counting
    /// launches.
    /// </summary>
    [Fact]
    public void Unpublishing_keeps_the_instant_the_country_first_went_live()
    {
        var directory = Create("MU", "Mauritius").Value;

        directory.Publish(Now, Curator);
        directory.Unpublish(Curator).IsSuccess.Should().BeTrue();

        directory.IsPublished.Should().BeFalse();
        directory.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public void Unpublishing_something_that_was_never_published_is_refused() =>
        Create("MU", "Mauritius").Value.Unpublish(Curator)
            .Error.Should().Be(KnowledgeErrors.DirectoryNotPublished);

    /// <summary>
    /// An archived directory is not a published one, whatever it was a moment ago — otherwise a
    /// consumer reading the archived row through an unfiltered query would conclude the country is
    /// live.
    /// </summary>
    [Fact]
    public void Archiving_also_takes_the_country_off_the_reader_surface()
    {
        var directory = Create("MU", "Mauritius").Value;
        directory.Publish(Now, Curator);

        directory.MarkDeleted(Now, Curator.Value.ToString());

        directory.IsDeleted.Should().BeTrue();
        directory.IsPublished.Should().BeFalse();
    }

    /// <summary>
    /// It was archived because somebody decided to stop serving the country. Putting it back in
    /// front of readers is a second decision that deserves a second look.
    /// </summary>
    [Fact]
    public void Restoring_returns_the_directory_unpublished_with_its_arrangement_intact()
    {
        var directory = Arranged("immigration", "housing");
        directory.Publish(Now, Curator);
        directory.MarkDeleted(Now, null);

        directory.Restore();

        directory.IsDeleted.Should().BeFalse();
        directory.IsPublished.Should().BeFalse();
        directory.Entries.Should().HaveCount(2);
    }

    // ── Localization and concurrency ────────────────────────────────────────────────────────

    [Fact]
    public void A_header_falls_back_from_locale_to_language_to_the_default()
    {
        var directory = Create("MU", "Mauritius").Value;

        directory.SetLocalizations([("fr", "Maurice", "Guide du pays.")], Curator);

        directory.Localized("fr-MU").DisplayName.Should().Be("Maurice");
        directory.Localized("sw").DisplayName.Should().Be("Mauritius");
    }

    [Fact]
    public void Declared_languages_round_trip_through_the_delimited_column()
    {
        var directory = CountryKnowledgeDirectory.Create(
            CountryKnowledgeDirectoryId.New(), "MU", "Mauritius", Now,
            languages: ["en", "FR", "en"]).Value;

        directory.Languages.Should().BeEquivalentTo(
            new[] { "en", "fr" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void A_write_from_a_stale_revision_is_refused()
    {
        var directory = Create("MU", "Mauritius").Value;

        directory.Arrange([("immigration", null, false)], Curator);

        directory.EnsureRevision(0).Error.Should().Be(KnowledgeErrors.RevisionConflict);
        directory.EnsureRevision(directory.Revision).IsSuccess.Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static Result<CountryKnowledgeDirectory> Create(string? country, string? name) =>
        CountryKnowledgeDirectory.Create(
            CountryKnowledgeDirectoryId.New(), country, name, Now, createdBy: Curator);

    private static CountryKnowledgeDirectory Arranged(params string[] keys)
    {
        var directory = Create("MU", "Mauritius").Value;

        directory.Arrange(
            [.. keys.Select(key => ((string?)key, (string?)null, false))], Curator);

        return directory;
    }

    private static Result Feature(
        CountryKnowledgeDirectory directory,
        GuideId guide,
        string? categoryKey = null,
        int? position = null,
        string? note = null) =>
        directory.FeatureGuide(
            Guid.NewGuid(), guide, categoryKey, position, note, Curator, Now);
}
