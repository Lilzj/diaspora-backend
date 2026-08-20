using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Knowledge;

/// <summary>
/// The Knowledge Directory's navigation catalogue.
/// <para>
/// <b>The distinction the whole phase rests on is asserted here.</b> A guide is <em>filed</em> under
/// a guide category by its author; a knowledge category is what a country page <em>shows</em>, and
/// it draws from one or more filing categories. Conflating them is how a knowledge base ends up
/// unable to rename a menu item without re-filing ten thousand guides.
/// </para>
/// </summary>
public class KnowledgeCategoryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly UserId Curator = UserId.From(Guid.NewGuid());

    // ── Creation ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_created_row_is_active_and_carries_its_normalised_key()
    {
        var category = Create("  Cost-Of-Living  ", "Cost of Living");

        category.IsSuccess.Should().BeTrue();
        category.Value.Key.Should().Be("cost-of-living");
        category.Value.Name.Should().Be("Cost of Living");
        category.Value.IsActive.Should().BeTrue();
        category.Value.IsDeleted.Should().BeFalse();
        category.Value.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Creation_raises_the_change_event_a_consumer_caches_navigation_from()
    {
        var category = Create("immigration", "Immigration").Value;

        var raised = category.DomainEvents
            .OfType<KnowledgeCategoryChangedDomainEvent>()
            .Single();

        raised.Change.Should().Be(KnowledgeChangeKinds.Created);
        raised.Key.Should().Be("immigration");
        raised.IsActive.Should().BeTrue();
        raised.ChangedBy.Should().Be(Curator);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("-leading")]
    [InlineData(null)]
    public void A_key_that_could_not_appear_in_a_url_is_refused(string? key)
    {
        var category = Create(key, "Anything");

        category.IsFailure.Should().BeTrue();
        category.Error.Should().Be(KnowledgeErrors.CategoryKeyInvalid);
    }

    [Fact]
    public void A_row_without_a_name_is_refused()
    {
        var category = Create("immigration", "   ");

        category.IsFailure.Should().BeTrue();
        category.Error.Should().Be(KnowledgeErrors.CategoryNameInvalid);
    }

    // ── Sources: the seam between curation and content ──────────────────────────────────────

    /// <summary>
    /// <b>The single most important behaviour in the aggregate.</b> A row with no declared sources
    /// draws from the filing category that shares its key, which is the common case and costs no
    /// rows. Expressing the fallback once, here, is what stops every discovery query reasoning about
    /// whether the collection is empty — and drifting when one of them forgets.
    /// </summary>
    [Fact]
    public void A_row_with_no_declared_sources_draws_from_the_filing_category_sharing_its_key()
    {
        var category = Create("housing", "Housing").Value;

        category.Sources.Should().BeEmpty();
        category.EffectiveSourceKeys.Should().BeEquivalentTo(["housing"]);
    }

    [Fact]
    public void A_row_can_draw_from_several_filing_categories()
    {
        var category = Create("student-life", "Student Life").Value;

        category.SetSources(["student-life", "education"], Curator).IsSuccess.Should().BeTrue();

        category.EffectiveSourceKeys.Should().BeEquivalentTo(["student-life", "education"]);
    }

    [Fact]
    public void A_repeated_source_collapses_rather_than_duplicating_a_shelf()
    {
        var category = Create("housing", "Housing").Value;

        category.SetSources(["housing", "HOUSING", " housing "], Curator);

        category.Sources.Should().ContainSingle();
    }

    /// <summary>
    /// Refused outright rather than dropped, so a curator never gets a silent partial save: a
    /// mistyped key that vanished would empty a shelf with nothing to diagnose from.
    /// </summary>
    [Fact]
    public void An_unusable_source_key_fails_the_whole_set()
    {
        var category = Create("housing", "Housing").Value;

        var result = category.SetSources(["housing", "not a key"], Curator);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(KnowledgeErrors.CategoryKeyInvalid);
        category.Sources.Should().BeEmpty("a refused batch changes nothing");
    }

    [Fact]
    public void More_sources_than_a_navigation_row_can_hold_are_refused()
    {
        var category = Create("housing", "Housing").Value;

        var many = Enumerable.Range(0, 20).Select(index => $"key-{index}").ToList();

        category.SetSources(many, Curator).Error.Should().Be(KnowledgeErrors.SourceLimit);
    }

    // ── Localization ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_label_resolves_to_the_exact_locale_when_one_exists()
    {
        var category = Localised();

        var (name, description) = category.Localized("fr-MU");

        name.Should().Be("Immigration mauricienne");
        description.Should().Be("Permis et titres de sejour.");
    }

    /// <summary>
    /// <b>The fallback that makes translations worth having.</b> A reader asking for Mauritian French
    /// gets plain French when nobody has written the former, which is right far more often than
    /// showing them English.
    /// </summary>
    [Fact]
    public void A_label_falls_back_to_the_language_before_it_falls_back_to_the_default()
    {
        var category = Localised();

        var (name, _) = category.Localized("fr-CA");

        name.Should().Be("Immigration");
    }

    [Fact]
    public void A_label_falls_back_to_the_default_for_a_language_nobody_translated()
    {
        var category = Localised();

        var (name, _) = category.Localized("sw");

        name.Should().Be("Immigration and Visas");
    }

    [Fact]
    public void Two_spellings_of_one_locale_are_one_translation()
    {
        var category = Create("immigration", "Immigration").Value;

        category.SetLocalizations(
            [("fr-mu", "Un", null), ("FR-MU", "Deux", null)], Curator);

        category.Localizations.Should().ContainSingle(
            "the aggregate normalises the tag, so the second is the same row as the first");

        category.Localizations[0].Name.Should().Be(
            "Un", "the first wins — a repeated row is a pasted spreadsheet, not a decision");
    }

    [Fact]
    public void An_unusable_locale_fails_the_whole_translation_set()
    {
        var category = Create("immigration", "Immigration").Value;

        var result = category.SetLocalizations([("fr", "Immigration", null), ("nope!", "X", null)], Curator);

        result.Error.Should().Be(KnowledgeErrors.LocaleInvalid);
        category.Localizations.Should().BeEmpty();
    }

    // ── Concurrency ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Curation is the one part of the platform where simultaneous editors are the norm — a small
    /// team, all at once, during the week a country launches.
    /// </summary>
    [Fact]
    public void A_write_from_a_stale_revision_is_refused()
    {
        var category = Create("immigration", "Immigration").Value;

        category.Update("Immigration and Visas", null, null, 20, Curator);

        category.EnsureRevision(0).Error.Should().Be(KnowledgeErrors.RevisionConflict);
        category.EnsureRevision(category.Revision).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_write_that_names_no_revision_is_allowed_through()
    {
        // A script has not read the row and is not claiming to have. A curation UI always sends one.
        Create("immigration", "Immigration").Value
            .EnsureRevision(null).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Every_mutation_bumps_the_revision()
    {
        var category = Create("immigration", "Immigration").Value;

        var start = category.Revision;

        category.Update("Immigration", null, null, 1, Curator);
        category.SetSources(["immigration"], Curator);
        category.SetProviderCategories(["lawyer"], Curator);
        category.SetLocalizations([("fr", "Immigration", null)], Curator);

        category.Revision.Should().Be(start + 4);
    }

    // ── Archiving is the soft delete ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>One "gone" state, deliberately.</b> An active flag beside a deleted flag would give two
    /// ways for a row to be absent, two queries that disagree about which, and a restore that fixes
    /// one of them.
    /// </summary>
    [Fact]
    public void Archiving_is_the_soft_delete_and_active_is_its_inverse()
    {
        var category = Create("immigration", "Immigration").Value;

        category.MarkDeleted(Now, Curator.Value.ToString());

        category.IsDeleted.Should().BeTrue();
        category.IsActive.Should().BeFalse();
        category.DeletedAt.Should().Be(Now);
    }

    [Fact]
    public void Archiving_raises_the_event_a_relay_and_a_cache_both_need()
    {
        var category = Create("immigration", "Immigration").Value;
        category.ClearDomainEvents();

        category.MarkDeleted(Now, Curator.Value.ToString());

        var raised = category.DomainEvents
            .OfType<KnowledgeCategoryChangedDomainEvent>()
            .Single();

        raised.Change.Should().Be(KnowledgeChangeKinds.Archived);
        raised.IsActive.Should().BeFalse();
        raised.ChangedBy.Should().Be(Curator, "the interceptor's actor string is parsed back");
    }

    [Fact]
    public void Restoring_returns_the_row_to_the_catalogue()
    {
        var category = Create("immigration", "Immigration").Value;
        category.MarkDeleted(Now, null);
        category.ClearDomainEvents();

        category.Restore();

        category.IsActive.Should().BeTrue();
        category.DeletedAt.Should().BeNull();

        category.DomainEvents.OfType<KnowledgeCategoryChangedDomainEvent>()
            .Single().Change.Should().Be(KnowledgeChangeKinds.Restored);
    }

    /// <summary>
    /// A non-member actor — a seeder, a background sweep — leaves the event's actor null rather than
    /// inventing one.
    /// </summary>
    [Fact]
    public void An_archival_by_something_that_is_not_a_member_names_nobody()
    {
        var category = Create("immigration", "Immigration").Value;
        category.ClearDomainEvents();

        category.MarkDeleted(Now, "system");

        category.DomainEvents.OfType<KnowledgeCategoryChangedDomainEvent>()
            .Single().ChangedBy.Should().BeNull();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static Result<KnowledgeCategory> Create(
        string? key, string? name) =>
        KnowledgeCategory.Create(
            KnowledgeCategoryId.New(), key, name, Now, createdBy: Curator);

    private static KnowledgeCategory Localised()
    {
        var category = Create("immigration", "Immigration and Visas").Value;

        category.SetLocalizations(
            [
                ("fr", "Immigration", null),
                ("fr-MU", "Immigration mauricienne", "Permis et titres de sejour.")
            ],
            Curator);

        return category;
    }
}
