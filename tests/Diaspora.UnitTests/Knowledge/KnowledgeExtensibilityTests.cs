using Diaspora.Modules.Guides;
using Diaspora.Modules.Guides.Extensibility.Knowledge;
using Diaspora.Modules.Guides.Infrastructure.Knowledge;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Knowledge;

/// <summary>
/// The seams that make the brief's section list a default rather than a ceiling.
/// <para>
/// <b>Two behaviours matter more than the rest.</b> A contributed provider that throws must cost the
/// reader one shelf and not the whole page — a category view composed of seven independent shelves
/// becoming a 500 because a contributed eighth has a bug is the failure mode a plug-in surface
/// cannot have. And a deployment replacing a shipped shelf must <em>replace</em> it, not add a second
/// one with the same heading.
/// </para>
/// </summary>
public class KnowledgeExtensibilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    // ── Section providers ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Providers_render_in_their_declared_order()
    {
        var registry = Registry(
            new StubSection("third", order: 30),
            new StubSection("first", order: 10),
            new StubSection("second", order: 20));

        registry.Keys.Should().BeEquivalentTo(
            new[] { "first", "second", "third" }, options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Last registration wins per key. A deployment replacing "most helpful" with its own ranking
    /// registers after B8.1 and gets the key; it does not get a second shelf with the same heading.
    /// </summary>
    [Fact]
    public async Task A_later_provider_replaces_an_earlier_one_claiming_the_same_key()
    {
        var registry = Registry(
            new StubSection("most-helpful", order: 20, title: "Shipped"),
            new StubSection("most-helpful", order: 20, title: "Replaced"));

        var sections = await registry.BuildAsync(Request());

        sections.Should().ContainSingle();
        sections[0].Title.Should().Be("Replaced");
    }

    /// <summary>
    /// <b>One broken shelf must not fail the page.</b> The reader wants the six that worked.
    /// </summary>
    [Fact]
    public async Task A_provider_that_throws_costs_one_shelf_and_not_the_page()
    {
        var registry = Registry(
            new StubSection("good", order: 10),
            new ThrowingSection(),
            new StubSection("also-good", order: 30));

        var sections = await registry.BuildAsync(Request());

        sections.Select(section => section.Key).Should().BeEquivalentTo(["good", "also-good"]);
    }

    /// <summary>
    /// A heading over an empty list tells a reader the platform has nothing for them in a way a
    /// missing heading does not.
    /// </summary>
    [Fact]
    public async Task An_empty_shelf_is_dropped_rather_than_rendered_as_a_heading()
    {
        var registry = Registry(
            new StubSection("empty", order: 10, guides: 0),
            new StubSection("full", order: 20, guides: 2));

        var sections = await registry.BuildAsync(Request());

        sections.Should().ContainSingle();
        sections[0].Key.Should().Be("full");
    }

    [Fact]
    public async Task A_provider_that_does_not_apply_is_never_asked()
    {
        var inapplicable = new StubSection("featured", order: 10) { Applies = false };

        var sections = await Registry(inapplicable).BuildAsync(Request());

        sections.Should().BeEmpty();
        inapplicable.Built.Should().BeFalse();
    }

    // ── Country context contributors ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_failing_vertical_costs_one_band_and_not_the_dashboard()
    {
        var registry = new CountryContextContributorRegistry(
            [new StubContext("Jobs", 10, 142), new ThrowingContext(), new StubContext("Housing", 20, 38)],
            NullLogger<CountryContextContributorRegistry>.Instance);

        var blocks = await registry.CollectAsync("MU");

        blocks.Select(block => block.Kind).Should().BeEquivalentTo(["jobs", "housing"]);
        registry.Modules.Should().Contain(["Jobs", "Housing", "Broken"]);
    }

    [Fact]
    public async Task A_band_with_nothing_in_it_is_not_rendered()
    {
        var registry = new CountryContextContributorRegistry(
            [new StubContext("Jobs", 10, count: 0)],
            NullLogger<CountryContextContributorRegistry>.Instance);

        (await registry.CollectAsync("MU")).Should().BeEmpty();
    }

    // ── The shipped catalogue and template ──────────────────────────────────────────────────

    /// <summary>
    /// Every navigation row the shipped catalogue declares must draw from a filing category that
    /// exists — either one B8 seeds, or one B8.1 contributes through B8's own seam. A row sourcing
    /// from a key nobody creates is a permanently empty shelf on every country page.
    /// </summary>
    [Fact]
    public void Every_shipped_navigation_row_draws_from_a_filing_category_that_exists()
    {
        var filingKeys = new Diaspora.Modules.Guides.Infrastructure.Categories
                .DiasporaGuideCategoryContributor()
            .GetCategories()
            .Select(seed => seed.Key)
            .Concat(new KnowledgeGuideCategoryContributor()
                .GetCategories()
                .Select(seed => seed.Key))
            .ToHashSet(StringComparer.Ordinal);

        var navigation = new DiasporaKnowledgeCategoryContributor().GetCategories().ToList();

        navigation.Should().NotBeEmpty();

        foreach (var row in navigation)
        {
            var sources = row.SourceCategoryKeys.Count == 0 ? [row.Key] : row.SourceCategoryKeys;

            sources.Should().OnlyContain(
                key => filingKeys.Contains(key),
                "navigation row '{0}' would render an empty shelf on every country page", row.Key);
        }
    }

    /// <summary>
    /// A template naming a row the catalogue does not declare would silently arrange nothing, which
    /// is the shape of bug a curator only finds by counting an empty page.
    /// </summary>
    [Fact]
    public void Every_templated_row_exists_in_the_shipped_catalogue()
    {
        var catalogue = new DiasporaKnowledgeCategoryContributor()
            .GetCategories()
            .Select(seed => seed.Key)
            .ToHashSet(StringComparer.Ordinal);

        var template = new DefaultCountryDirectoryTemplateContributor()
            .GetTemplates()
            .Single();

        template.KnowledgeCategoryKeys.Concat(template.HiddenKeys)
            .Should().OnlyContain(key => catalogue.Contains(key));

        template.KnowledgeCategoryKeys.Should().StartWith(
            "getting-started",
            "arriving somewhere begins with getting oriented, not with banking");
    }

    [Fact]
    public void The_shipped_catalogue_declares_no_duplicate_keys()
    {
        var keys = new DiasporaKnowledgeCategoryContributor()
            .GetCategories()
            .Select(seed => seed.Key)
            .ToList();

        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void The_contributed_filing_categories_do_not_collide_with_the_ones_b8_seeds()
    {
        var existing = new Diaspora.Modules.Guides.Infrastructure.Categories
                .DiasporaGuideCategoryContributor()
            .GetCategories()
            .Select(seed => seed.Key)
            .ToHashSet(StringComparer.Ordinal);

        new KnowledgeGuideCategoryContributor().GetCategories()
            .Should().OnlyContain(
                seed => !existing.Contains(seed.Key),
                "the seeder matches on key, so a collision silently drops the contribution");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static KnowledgeSectionProviderRegistry Registry(
        params IKnowledgeSectionProvider[] providers) =>
        new(providers, NullLogger<KnowledgeSectionProviderRegistry>.Instance);

    private static KnowledgeSectionRequest Request() => new()
    {
        CountryCode = "MU",
        KnowledgeCategoryKey = "housing",
        GuideCategoryKeys = ["housing"],
        Limit = 6,
        At = Now
    };

    private sealed class StubSection : IKnowledgeSectionProvider
    {
        private readonly int _guides;
        private readonly string _title;

        public StubSection(string key, int order, string? title = null, int guides = 1)
        {
            Key = key;
            Order = order;
            _title = title ?? key;
            _guides = guides;
        }

        public string Key { get; }

        public int Order { get; }

        public bool Applies { get; init; } = true;

        public bool Built { get; private set; }

        public bool AppliesTo(KnowledgeSectionRequest request) => Applies;

        public Task<KnowledgeSectionContent?> BuildAsync(
            KnowledgeSectionRequest request, CancellationToken cancellationToken = default)
        {
            Built = true;

            return Task.FromResult<KnowledgeSectionContent?>(new KnowledgeSectionContent
            {
                Key = Key,
                Title = _title,
                GuideIds = [.. Enumerable.Range(0, _guides).Select(_ => Guid.NewGuid())]
            });
        }
    }

    private sealed class ThrowingSection : IKnowledgeSectionProvider
    {
        public string Key => "broken";

        public int Order => 20;

        public Task<KnowledgeSectionContent?> BuildAsync(
            KnowledgeSectionRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("a contributed shelf with a bug in it");
    }

    private sealed class StubContext : ICountryContextContributor
    {
        private readonly int _count;

        public StubContext(string module, int order, int count)
        {
            Module = module;
            Order = order;
            _count = count;
        }

        public string Module { get; }

        public int Order { get; }

        public Task<IReadOnlyList<CountryContextBlock>> GetBlocksAsync(
            string countryCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CountryContextBlock>>(
            [
                new CountryContextBlock
                {
                    Kind = Module.ToLowerInvariant(),
                    Label = Module,
                    Count = _count,
                    Order = Order
                }
            ]);
    }

    private sealed class ThrowingContext : ICountryContextContributor
    {
        public string Module => "Broken";

        public Task<IReadOnlyList<CountryContextBlock>> GetBlocksAsync(
            string countryCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("a vertical mid-deploy");
    }
}
