using Diaspora.Modules.Guides.Domain.CountryContext;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Knowledge;

/// <summary>
/// The counters behind a country dashboard, and the projection behind its contextual bands.
/// <para>
/// <b>The most important assertion in this file is that counters raise no domain events.</b> A
/// country page viewed ten thousand times must produce ten thousand increments and zero messages —
/// B8 records the same rule for guide views, and the reason is that an event per view turns a
/// popular country into a self-inflicted denial of service against the outbox.
/// </para>
/// </summary>
public class KnowledgeAnalyticsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    // ── Usage counters ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Counters_raise_no_domain_events_however_many_times_they_move()
    {
        var analytics = Start();

        for (var index = 0; index < 1_000; index++)
        {
            analytics.RecordDirectoryView(Now);
            analytics.RecordCategoryView("housing", Now);
            analytics.RecordGuideDiscovery(Now);
        }

        analytics.DirectoryViews.Should().Be(1_000);
        analytics.DomainEvents.Should().BeEmpty(
            "an event per view would put a thousand messages on the bus for one busy afternoon");
    }

    [Fact]
    public void A_category_view_increments_the_country_total_and_its_own_row()
    {
        var analytics = Start();

        analytics.RecordCategoryView("housing", Now);
        analytics.RecordCategoryView("housing", Now);
        analytics.RecordCategoryView("immigration", Now);

        analytics.CategoryViews.Should().Be(3);
        analytics.CategoryBreakdown.Should().HaveCount(2);

        analytics.CategoryBreakdown
            .Single(view => view.KnowledgeCategoryKey == "housing")
            .Views.Should().Be(2);
    }

    /// <summary>
    /// The two are deliberately not derived from one another: the total has to survive a category
    /// being renamed or archived, and the breakdown has to survive the total being reset by a future
    /// retention policy.
    /// </summary>
    [Fact]
    public void An_unusable_category_key_still_counts_towards_the_country_total()
    {
        var analytics = Start();

        analytics.RecordCategoryView("not a key", Now);

        analytics.CategoryViews.Should().Be(1);
        analytics.CategoryBreakdown.Should().BeEmpty();
    }

    [Fact]
    public void The_rates_are_computed_rather_than_stored()
    {
        var analytics = Start();

        analytics.RecordDirectoryView(Now);
        analytics.RecordDirectoryView(Now);
        analytics.RecordCategoryView("housing", Now);
        analytics.RecordCategoryView("housing", Now);
        analytics.RecordGuideDiscovery(Now);
        analytics.RecordGuideCompletion(Now);

        analytics.DiscoveryRate.Should().Be(0.5, "one guide opened per two country arrivals");
        analytics.ClickThroughRate.Should().Be(0.25, "one guide opened per four impressions");
        analytics.CompletionRate.Should().Be(1.0, "the one guide opened was read to the end");
    }

    /// <summary>
    /// Zero rather than undefined. A client should never have to guard against a division that
    /// already happened on the server.
    /// </summary>
    [Fact]
    public void Every_rate_is_zero_before_anybody_has_arrived()
    {
        var analytics = Start();

        analytics.DiscoveryRate.Should().Be(0);
        analytics.ClickThroughRate.Should().Be(0);
        analytics.CompletionRate.Should().Be(0);
        analytics.PathCompletionRate.Should().Be(0);
    }

    [Fact]
    public void Learning_path_completion_is_measured_against_starts()
    {
        var analytics = Start();

        analytics.RecordLearningPathStart(Now);
        analytics.RecordLearningPathStart(Now);
        analytics.RecordLearningPathStart(Now);
        analytics.RecordLearningPathStart(Now);
        analytics.RecordLearningPathCompletion(Now);

        analytics.PathCompletionRate.Should().Be(0.25);
    }

    // ── Search phrases ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Two spellings of one phrase are two rows that each look unimportant and are collectively the
    /// top search on the platform.
    /// </summary>
    [Theory]
    [InlineData("  Work   Permit  Renewal ", "work permit renewal")]
    [InlineData("WORK PERMIT RENEWAL", "work permit renewal")]
    public void A_search_phrase_is_normalised_before_it_is_counted(string input, string expected) =>
        KnowledgeSearchTerm.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    public void Noise_is_not_counted_as_a_phrase(string? term) =>
        KnowledgeSearchTerm.Normalize(term).Should().BeNull();

    [Fact]
    public void A_phrase_that_finds_nothing_is_the_commissioning_queue()
    {
        var term = KnowledgeSearchTerm.Start(
            KnowledgeSearchTermId.New(), "MU", "work permit renewal", resultCount: 0, Now)!;

        term.IsUnanswered.Should().BeTrue();
        term.SearchCount.Should().Be(1);

        term.Record(resultCount: 0, Now);
        term.SearchCount.Should().Be(2);

        // The moment somebody writes the missing guide, the answer changes on the next search
        // rather than decaying towards it over a month of averaging.
        term.Record(resultCount: 3, Now);
        term.IsUnanswered.Should().BeFalse();
        term.ResultCount.Should().Be(3);
    }

    /// <summary>
    /// The empty string rather than null, so the unique index over (country, term) works on every
    /// provider — a nullable column in a unique index treats every null as distinct on several of
    /// them, which would turn one global phrase into a fresh row on every search.
    /// </summary>
    [Fact]
    public void A_phrase_searched_without_a_country_is_stored_against_the_empty_string()
    {
        var term = KnowledgeSearchTerm.Start(
            KnowledgeSearchTermId.New(), null, "work permit", 1, Now)!;

        term.CountryCode.Should().BeEmpty();
    }

    // ── Cross-vertical projection ───────────────────────────────────────────────────────────

    [Fact]
    public void A_counter_appears_on_first_sight_and_accumulates()
    {
        var summary = CountryContextSummary.Start(
            CountryContextSummaryId.New(), "MU", Now);

        summary.Apply(CountryContextKind.Jobs, 1, Now).Should().Be(1);
        summary.Apply(CountryContextKind.Jobs, 1, Now).Should().Be(2);

        summary.CountOf(CountryContextKind.Jobs).Should().Be(2);
        summary.CountOf(CountryContextKind.Housing).Should().Be(0, "nothing has reported it");
    }

    /// <summary>
    /// <b>Clamping is what makes an at-least-once projection safe to look at.</b> A duplicate
    /// decrement, an event replayed after a redeploy, a vertical publishing an archive for something
    /// it never published — the worst case has to be a slightly low number rather than a country
    /// dashboard reporting minus three homes.
    /// </summary>
    [Fact]
    public void A_counter_never_goes_below_zero()
    {
        var summary = CountryContextSummary.Start(
            CountryContextSummaryId.New(), "MU", Now);

        summary.Apply(CountryContextKind.Housing, 1, Now);
        summary.Apply(CountryContextKind.Housing, -5, Now).Should().Be(0);
    }

    /// <summary>
    /// The kind is a string, so a vertical landing after B8.1 contributes a counter without a
    /// migration here.
    /// </summary>
    [Fact]
    public void A_counter_kind_the_platform_has_never_heard_of_is_accepted()
    {
        var summary = CountryContextSummary.Start(
            CountryContextSummaryId.New(), "MU", Now);

        summary.Apply("scholarships", 4, Now).Should().Be(4);
        summary.CountOf("scholarships").Should().Be(4);
    }

    [Fact]
    public void A_counter_can_be_rebuilt_from_an_authoritative_count()
    {
        var summary = CountryContextSummary.Start(
            CountryContextSummaryId.New(), "MU", Now);

        summary.Apply(CountryContextKind.Jobs, 7, Now);
        summary.Set(CountryContextKind.Jobs, 142, Now).Should().Be(142);
    }

    [Fact]
    public void The_projection_raises_no_domain_events_either()
    {
        var summary = CountryContextSummary.Start(
            CountryContextSummaryId.New(), "MU", Now);

        summary.Apply(CountryContextKind.Jobs, 1, Now);

        summary.DomainEvents.Should().BeEmpty(
            "the dashboard contract is published by the projector, once, when a counter moves");
    }

    private static CountryKnowledgeAnalytics Start() =>
        CountryKnowledgeAnalytics.Start(CountryKnowledgeAnalyticsId.New(), "MU", Now);
}
