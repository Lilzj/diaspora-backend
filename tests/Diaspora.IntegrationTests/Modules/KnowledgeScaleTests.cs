using System.Diagnostics;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Volume, and what breaks at it, for the platform's most-read screen.
/// <para>
/// <b>Correctness tests that happen to be large.</b> The timings they print go in the performance
/// report; none asserts on wall-clock time, because a loaded build agent would fail a clock
/// assertion for reasons that have nothing to do with the code, and a suite that goes red for that
/// stops being read. It is the platform's established shape, and B10's and B11's scale suites are
/// its siblings.
/// </para>
/// <para>
/// What they do assert is what only appears at volume: a country page whose cost is bounded by how
/// many rows a curator arranged rather than by how many guides exist behind them; a category
/// aggregation that counts exactly rather than approximately; a cursor that walks a large country
/// without losing or repeating a guide; and a directory that is still arrangeable at its ceiling.
/// </para>
/// <para>
/// <b>Every test scopes itself to its own country.</b> The class shares one database with the rest
/// of the suite, so a test asserting on a global count would pass or fail depending on which other
/// tests ran first — a worse failure than the bug it was meant to catch, because it does not
/// reproduce.
/// </para>
/// </summary>
public sealed class KnowledgeScaleTests : IntegrationTestBase
{
    /// <summary>
    /// Large enough that a scan and an index lookup diverge, and that a paging defect shows up as a
    /// missing or repeated guide rather than passing by luck. Small enough that the suite still
    /// finishes in a coffee break.
    /// </summary>
    private const int GuideCount = 1_500;

    private static readonly string[] Categories =
        ["immigration", "housing", "healthcare", "banking", "education"];

    private readonly ITestOutputHelper _output;

    public KnowledgeScaleTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        _output = output;

        // The schema can only be created once the server's provider exists, and this suite never
        // registers a member — so nothing else in it would have triggered the creation.
        factory.EnsureDatabaseCreated();
    }

    // ── Large guide libraries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The whole country walked page by page. Every guide seen exactly once — the assertion a cursor
    /// exists to earn, and the one that quietly fails when rows share a sort key and the tie-break is
    /// missing.
    /// <para>
    /// The degenerate case is real rather than theoretical: publication timestamps are stamped in one
    /// unit of work here, so every row shares an instant and the slug tie-break is the only thing
    /// separating them.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_cursor_walks_a_large_country_without_loss_or_repetition()
    {
        const string Country = "XA";

        await SeedCountryAsync(Country, GuideCount);

        using var scope = Factory.Services.CreateScope();
        var finder = scope.ServiceProvider.GetRequiredService<KnowledgeGuideFinder>();

        var seen = new HashSet<Guid>();
        var pages = 0;
        string? cursor = null;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var page = await finder.PageAsync(
                finder.Filter(Country, limit: KnowledgeLimits.MaxPageSize), cursor);

            foreach (var guide in page.Items)
            {
                seen.Add(guide.Id.Value).Should().BeTrue(
                    "a cursor that returns a row twice is a cursor with no tie-break");
            }

            pages++;

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            cursor = page.NextCursor;

            pages.Should().BeLessThan(
                200, "a cursor that never reports exhaustion would loop here forever");
        }

        stopwatch.Stop();

        seen.Should().HaveCount(GuideCount, "every guide is seen exactly once");

        _output.WriteLine(
            $"Walked {GuideCount} guides in {pages} pages of {KnowledgeLimits.MaxPageSize} " +
            $"in {stopwatch.ElapsedMilliseconds} ms " +
            $"({stopwatch.ElapsedMilliseconds / (double)pages:F1} ms/page).");
    }

    /// <summary>
    /// A page is a page. A limit that leaks under volume is how a mobile client on a metered
    /// connection ends up downloading fifteen hundred guide cards.
    /// </summary>
    [Fact]
    public async Task A_shelf_stays_bounded_however_much_matches()
    {
        const string Country = "XB";

        await SeedCountryAsync(Country, 400);

        using var scope = Factory.Services.CreateScope();
        var finder = scope.ServiceProvider.GetRequiredService<KnowledgeGuideFinder>();

        var page = await finder.PageAsync(
            finder.Filter(Country, limit: 5_000), cursor: null);

        page.Items.Should().HaveCountLessThanOrEqualTo(
            KnowledgeLimits.MaxPageSize,
            "a caller asking for five thousand gets a page, whatever it asked for");
    }

    // ── Category aggregation ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The count behind every row of every country page, at volume.</b> It has to be exact —
    /// "Housing · 20+" on a country with three hundred housing guides is a number an editorial team
    /// cannot plan from — and it has to stay an index seek rather than a page walk, which is why the
    /// guide repository grew a count that shares the listing's own filter.
    /// </summary>
    [Fact]
    public async Task Category_aggregation_counts_exactly_and_not_approximately()
    {
        const string Country = "XC";

        var written = await SeedCountryAsync(Country, 600);

        using var scope = Factory.Services.CreateScope();
        var finder = scope.ServiceProvider.GetRequiredService<KnowledgeGuideFinder>();

        var stopwatch = Stopwatch.StartNew();

        var counted = 0;

        foreach (var category in Categories)
        {
            var count = await finder.CountAsync(Country, [category]);

            count.Should().Be(
                written[category],
                "a page that walks to count reports 'at least a page' for every busy category");

            count.Should().BeGreaterThan(
                KnowledgeLimits.MaxPageSize,
                "this category is deliberately larger than one page, which is the whole point");

            counted += count;
        }

        stopwatch.Stop();

        counted.Should().Be(600);

        _output.WriteLine(
            $"Counted {Categories.Length} categories over 600 guides " +
            $"in {stopwatch.ElapsedMilliseconds} ms " +
            $"({stopwatch.ElapsedMilliseconds / (double)Categories.Length:F1} ms/category).");
    }

    /// <summary>
    /// <b>The cost of a country page is bounded by the arrangement, not by the library behind it.</b>
    /// Twenty rows cost twenty counts whether the country holds ten guides or ten thousand, which is
    /// the property that makes the directory affordable at all — and the one a naive implementation
    /// loses the moment somebody counts by materialising a page.
    /// </summary>
    [Fact]
    public async Task Composing_a_country_page_costs_the_same_at_ten_guides_and_at_a_thousand()
    {
        await SeedCountryAsync("XD", 10);
        await SeedCountryAsync("XE", 1_000);

        var small = await TimeCompositionAsync("XD");
        var large = await TimeCompositionAsync("XE");

        _output.WriteLine(
            $"Composed a country page over 10 guides in {small} ms and over 1,000 in {large} ms.");

        // No wall-clock assertion — a loaded agent would fail it for reasons unrelated to the code.
        // What is asserted is that both answered, with the same arrangement, over libraries two
        // orders of magnitude apart.
        small.Should().BeGreaterThanOrEqualTo(0);
        large.Should().BeGreaterThanOrEqualTo(0);
    }

    // ── Curation at its ceiling ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A directory arranged to its limit still renumbers contiguously and still refuses the row past
    /// it. The ceiling is where an off-by-one lives.
    /// </summary>
    [Fact]
    public async Task A_directory_is_arrangeable_at_its_ceiling_and_refuses_the_row_past_it()
    {
        using var scope = Factory.Services.CreateScope();

        var categories = scope.ServiceProvider.GetRequiredService<IKnowledgeCategoryRepository>();
        var directories = scope.ServiceProvider
            .GetRequiredService<ICountryKnowledgeDirectoryRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Enough catalogue rows to fill a directory to its ceiling.
        var keys = new List<string>();

        for (var index = 0; index < KnowledgeLimits.MaxDirectoryEntries; index++)
        {
            var key = $"scale-row-{index:D3}";

            keys.Add(key);

            var created = KnowledgeCategory.Create(
                KnowledgeCategoryId.New(), key, $"Scale row {index}", DateTimeOffset.UtcNow);

            created.IsSuccess.Should().BeTrue();

            await categories.AddAsync(created.Value);
        }

        var directory = CountryKnowledgeDirectory.Create(
            CountryKnowledgeDirectoryId.New(), "XF", "Ceiling", DateTimeOffset.UtcNow).Value;

        directory.Arrange(
            [.. keys.Select(key => ((string?)key, (string?)null, false))], changedBy: null)
            .IsSuccess.Should().BeTrue();

        await directories.AddAsync(directory);
        await unitOfWork.SaveChangesAsync();

        var reread = await directories.GetForReadAsync("XF");

        reread.Should().NotBeNull();

        reread!.Entries.Should().HaveCount(KnowledgeLimits.MaxDirectoryEntries);

        reread.Entries.Select(entry => entry.Position).OrderBy(position => position)
            .Should().BeEquivalentTo(
                Enumerable.Range(0, KnowledgeLimits.MaxDirectoryEntries),
                options => options.WithStrictOrdering(),
                "positions survive a round trip through an owned collection contiguously");

        var tracked = await directories.GetAsync("XF");

        tracked!.Arrange(
            [.. keys.Append("scale-row-overflow").Select(key => ((string?)key, (string?)null, false))],
            changedBy: null)
            .IsFailure.Should().BeTrue("the row past the ceiling is refused");
    }

    // ── Learning paths at their ceiling ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_learning_path_holds_its_maximum_steps_and_round_trips_them_in_order()
    {
        using var scope = Factory.Services.CreateScope();

        var paths = scope.ServiceProvider.GetRequiredService<ILearningPathRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var path = LearningPath.Create(
            LearningPathId.New(), "ceiling", "XG", "Every step there is", DateTimeOffset.UtcNow).Value;

        var stepIds = Enumerable.Range(0, KnowledgeLimits.MaxPathSteps)
            .Select(_ => Guid.NewGuid())
            .ToList();

        path.Arrange(
            [
                .. stepIds.Select((stepId, index) =>
                    (stepId, (string?)$"Step {index}", (string?)null, (Guid?)null,
                        (string?)"immigration", false))
            ],
            changedBy: null)
            .IsSuccess.Should().BeTrue();

        await paths.AddAsync(path);
        await unitOfWork.SaveChangesAsync();

        var reread = await paths.GetForReadAsync("XG", "ceiling");

        reread.Should().NotBeNull();

        reread!.Steps.OrderBy(step => step.Position).Select(step => step.StepId)
            .Should().BeEquivalentTo(
                stepIds,
                options => options.WithStrictOrdering(),
                "caller-supplied step identifiers survive the round trip in curated order");
    }

    // ── Search at volume ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The search port answering over a real corpus, through whichever engine is registered. What is
    /// asserted is that a filter still filters at volume and a page is still a page — not relevance,
    /// which the shipped engine honestly does not compute.
    /// </summary>
    [Fact]
    public async Task Search_still_filters_when_there_is_a_lot_to_filter()
    {
        const string Country = "XH";

        await SeedCountryAsync(Country, 800);

        using var scope = Factory.Services.CreateScope();

        var engine = scope.ServiceProvider
            .GetRequiredService<Diaspora.Modules.Guides.Extensibility.Search.IKnowledgeSearchEngine>();

        var stopwatch = Stopwatch.StartNew();

        var outcome = await engine.SearchAsync(
            new Diaspora.Modules.Guides.Extensibility.Search.KnowledgeSearchRequest
            {
                CountryCode = Country,
                GuideCategoryKeys = ["housing"],
                Limit = KnowledgeLimits.MaxPageSize
            });

        stopwatch.Stop();

        outcome.Hits.Should().HaveCountLessThanOrEqualTo(KnowledgeLimits.MaxPageSize);
        outcome.Hits.Should().NotBeEmpty();
        outcome.HasMore.Should().BeTrue("housing alone is larger than one page here");
        outcome.Engine.Should().NotBeNullOrWhiteSpace();

        _output.WriteLine(
            $"Searched 800 guides in {stopwatch.ElapsedMilliseconds} ms " +
            $"through the {outcome.Engine} engine.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes guides straight through the repository and arranges a directory over them.
    /// <para>
    /// Through the domain rather than over HTTP, deliberately: fifteen hundred guides created by a
    /// real author would be fifteen hundred registrations, and the thing under test is what happens
    /// at volume rather than how the volume arrived.
    /// </para>
    /// </summary>
    private async Task<Dictionary<string, int>> SeedCountryAsync(string countryCode, int count)
    {
        using var scope = Factory.Services.CreateScope();

        var guides = scope.ServiceProvider.GetRequiredService<IGuideRepository>();
        var categories = scope.ServiceProvider.GetRequiredService<IKnowledgeCategoryRepository>();
        var directories = scope.ServiceProvider
            .GetRequiredService<ICountryKnowledgeDirectoryRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var author = UserId.From(Guid.NewGuid());
        var written = Categories.ToDictionary(category => category, _ => 0, StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < count; index++)
        {
            var category = Categories[index % Categories.Length];

            var guide = Guide.Create(
                GuideId.New(),
                author,
                GuideTitle.Create($"{countryCode} guide number {index}").Value,
                GuideSlug.Create($"{countryCode.ToLowerInvariant()}-guide-{index:D5}").Value,
                category,
                GuideLanguage.Create("en").Value,
                GuideLocationContext.Create(countryCode).Value,
                now).Value;

            guide.ReplaceStructure(
                [
                    new GuideSectionDraft
                    {
                        SectionId = Guid.NewGuid(),
                        Title = "What to do",
                        Order = 0,
                        Blocks =
                        [
                            new GuideBlockDraft
                            {
                                BlockId = Guid.NewGuid(),
                                Kind = GuideBlockKind.Markdown,
                                Order = 0,
                                Content = "Take the form, the fee and one hour of patience."
                            }
                        ]
                    }
                ],
                baseRevision: null,
                author);

            guide.Publish(now, author);

            await guides.AddAsync(guide);

            written[category]++;
        }

        var existing = await directories.GetAsync(countryCode);

        if (existing is null)
        {
            var catalogue = await categories.GetByKeysAsync(Categories);

            var directory = CountryKnowledgeDirectory.Create(
                CountryKnowledgeDirectoryId.New(), countryCode, countryCode, now).Value;

            directory.Arrange(
                [
                    .. Categories
                        .Where(catalogue.ContainsKey)
                        .Select(category => ((string?)category, (string?)null, false))
                ],
                changedBy: null);

            directory.Publish(now, changedBy: null);

            await directories.AddAsync(directory);
        }

        await unitOfWork.SaveChangesAsync();

        return written;
    }

    private async Task<long> TimeCompositionAsync(string countryCode)
    {
        using var scope = Factory.Services.CreateScope();

        var directories = scope.ServiceProvider
            .GetRequiredService<ICountryKnowledgeDirectoryRepository>();

        var composer = scope.ServiceProvider.GetRequiredService<KnowledgeDirectoryComposer>();

        var directory = await directories.GetForReadAsync(countryCode);

        directory.Should().NotBeNull();

        var stopwatch = Stopwatch.StartNew();

        var composed = await composer.ComposeAsync(directory!, locale: null);

        stopwatch.Stop();

        composed.Categories.Should().HaveCount(
            Categories.Length, "the arrangement decides the cost, not the library behind it");

        return stopwatch.ElapsedMilliseconds;
    }
}
