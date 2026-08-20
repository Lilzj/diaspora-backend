using System.Diagnostics;
using System.Globalization;
using Diaspora.Contracts.Authorization;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Feedback;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Domain.Signals;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Engine;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Volume, and what breaks at it, for the platform's intelligence layer.
/// <para>
/// <b>Correctness tests that happen to be large.</b> The timings they print go in the performance
/// report; none asserts on wall-clock time, because a loaded build agent would fail a clock
/// assertion for reasons that have nothing to do with the code, and a suite that goes red for that
/// stops being read. B10's, B11's, B8.1's and B13's scale suites are its siblings.
/// </para>
/// <para>
/// What they do assert is what only appears at volume: a ranking pass whose cost is bounded by the
/// pool size rather than by the corpus; a profile build over thousands of signals that completes and
/// produces a bounded profile; and a generation whose page is the right size however large the
/// candidate table gets.
/// </para>
/// <para>
/// <b>Every test scopes itself to its own member.</b> The class shares one database with the rest of
/// the suite, so a test asserting on a global count would pass or fail depending on which other tests
/// ran first — a worse failure than the bug it was meant to catch, because it does not reproduce.
/// </para>
/// </summary>
public sealed class RecommendationScaleTests : IntegrationTestBase
{
    /// <summary>
    /// Large enough that the pool bound matters and a full scan and an indexed read diverge. Small
    /// enough that the suite still finishes.
    /// </summary>
    private const int CandidateCount = 2_000;

    /// <summary>Enough that a profile build is a real pass rather than a formality.</summary>
    private const int SignalCount = 2_000;

    private readonly ITestOutputHelper _output;

    public RecommendationScaleTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        factory.EnsureDatabaseCreated();
        _output = output;
    }

    /// <summary>
    /// <b>A large candidate table does not make a page large.</b> The pool bound is what keeps the
    /// ranking pass's cost independent of how much content the platform has — without it, every
    /// strategy would score the whole corpus and the surface would get slower every week.
    /// </summary>
    [Fact]
    public async Task Generation_over_a_large_corpus_returns_a_bounded_page()
    {
        var userId = UserId.New();

        await SeedCandidatesAsync(CandidateCount, "MU");
        await SeedProfileAsync(userId, "MU");

        var stopwatch = Stopwatch.StartNew();

        var result = await GenerateAsync(userId, RecommendationSurface.Guides, limit: 20);

        stopwatch.Stop();

        result.Items.Should().HaveCountLessThanOrEqualTo(20);
        result.Items.Should().NotBeEmpty();

        _output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Ranked a page from {CandidateCount} candidates in {stopwatch.ElapsedMilliseconds} ms."));
    }

    /// <summary>
    /// <b>Every item on a large page is still explained.</b> The explanation is built per item, so a
    /// defect that only appeared past a certain page size — a null reason on the tail — would never
    /// show up in a test that ranked three candidates.
    /// </summary>
    [Fact]
    public async Task Every_item_on_a_full_page_carries_an_explanation()
    {
        var userId = UserId.New();

        await SeedCandidatesAsync(500, "GB");
        await SeedProfileAsync(userId, "GB");

        var result = await GenerateAsync(userId, RecommendationSurface.Guides, limit: 100);

        result.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(item =>
            item.Explanation != null &&
            !string.IsNullOrWhiteSpace(item.Explanation.ResourceKey) &&
            !string.IsNullOrWhiteSpace(item.Explanation.FallbackText));
    }

    /// <summary>
    /// <b>A profile built from thousands of signals stays bounded.</b> The interest ceiling is what
    /// stops a profile growing without limit — and a profile that grew would make every generation
    /// slower for exactly the members who use the platform most, which is backwards.
    /// </summary>
    [Fact]
    public async Task A_profile_built_from_thousands_of_signals_stays_bounded()
    {
        var userId = UserId.New();

        await SeedSignalsAsync(userId, SignalCount);

        var stopwatch = Stopwatch.StartNew();

        using var scope = Factory.Services.CreateScope();
        var builder = scope.ServiceProvider.GetRequiredService<IPersonalizationProfileBuilder>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var profile = await builder.BuildIncrementalAsync(userId, CancellationToken.None);

        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        stopwatch.Stop();

        profile.Interests.Should().HaveCountLessThanOrEqualTo(PersonalizationProfile.MaximumInterests);
        profile.LocationAffinities.Should()
            .HaveCountLessThanOrEqualTo(PersonalizationProfile.MaximumLocationAffinities);

        profile.SignalCount.Should().BeGreaterThan(0);

        _output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Folded {SignalCount} signals in {stopwatch.ElapsedMilliseconds} ms; "
            + $"{profile.Interests.Count} interests, completeness {profile.Completeness}."));
    }

    /// <summary>
    /// <b>Folding is incremental, so a second build over the same history is nearly free.</b> Without
    /// the processed stamp every build would re-read a member's whole history, and the platform's
    /// most engaged members would have the slowest feed.
    /// </summary>
    [Fact]
    public async Task A_second_build_finds_nothing_left_to_fold()
    {
        var userId = UserId.New();

        await SeedSignalsAsync(userId, 500);

        using (var first = Factory.Services.CreateScope())
        {
            await first.ServiceProvider
                .GetRequiredService<IPersonalizationProfileBuilder>()
                .BuildIncrementalAsync(userId, CancellationToken.None);

            await first.ServiceProvider
                .GetRequiredService<IUnitOfWork>()
                .SaveChangesAsync(CancellationToken.None);
        }

        using var scope = Factory.Services.CreateScope();

        var signals = scope.ServiceProvider.GetRequiredService<ISignalRepository>();

        var remaining = await signals.ListUnprocessedAsync(userId, 1000, CancellationToken.None);

        remaining.Should().BeEmpty("a folded signal is stamped, so the next pass skips it");
    }

    /// <summary>
    /// <b>Reusing a fresh page is cheaper than ranking again.</b> Three pull-to-refreshes in ten
    /// seconds should not be three ranking passes — the timing is printed rather than asserted, but
    /// the reuse itself is.
    /// </summary>
    [Fact]
    public async Task A_repeat_request_reuses_the_stored_page()
    {
        var userId = UserId.New();

        await SeedCandidatesAsync(200, "NG");
        await SeedProfileAsync(userId, "NG");

        var first = await GenerateAsync(userId, RecommendationSurface.Guides, limit: 10);

        first.WasCached.Should().BeFalse();

        var stopwatch = Stopwatch.StartNew();

        var second = await GenerateAsync(userId, RecommendationSurface.Guides, limit: 10);

        stopwatch.Stop();

        second.WasCached.Should().BeTrue("the first page has not expired");
        second.Items.Should().HaveCount(first.Items.Count);

        _output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Reused a stored page in {stopwatch.ElapsedMilliseconds} ms."));
    }

    /// <summary>
    /// <b>A member with thousands of dismissals still gets a page.</b> The suppression set is read
    /// once per generation and applied as a hash lookup; a per-candidate query would make this the
    /// slowest path in the module for exactly the members who use it most.
    /// </summary>
    [Fact]
    public async Task A_large_suppression_set_does_not_empty_the_page()
    {
        var userId = UserId.New();

        await SeedCandidatesAsync(400, "KE");
        await SeedProfileAsync(userId, "KE");

        // Dismiss things that are not in the pool: the point is the size of the set, not its content.
        await SeedDismissalsAsync(userId, 1_000);

        var stopwatch = Stopwatch.StartNew();

        var result = await GenerateAsync(userId, RecommendationSurface.Guides, limit: 20);

        stopwatch.Stop();

        result.Items.Should().NotBeEmpty();

        _output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Ranked against 1,000 suppressions in {stopwatch.ElapsedMilliseconds} ms."));
    }

    // ── Fixtures ─────────────────────────────────────────────────────────

    private async Task<RecommendationResult> GenerateAsync(
        UserId userId, RecommendationSurface surface, int limit)
    {
        using var scope = Factory.Services.CreateScope();

        var engine = scope.ServiceProvider.GetRequiredService<IRecommendationEngine>();

        return await engine.GenerateAsync(
            new RecommendationRequest
            {
                UserId = userId,
                Surface = surface,
                TargetType = RecommendationTargetType.Guide,
                Limit = limit,
                AccessTier = AccessTier.Limited
            },
            CancellationToken.None);
    }

    private async Task SeedCandidatesAsync(int count, string countryCode)
    {
        using var scope = Factory.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IRecommendationCandidateRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        for (var index = 0; index < count; index++)
        {
            var candidate = await repository.GetOrProjectAsync(
                RecommendationTargetType.Guide,
                Guid.NewGuid(),
                "Guides",
                $"Guide {index}",
                CancellationToken.None);

            candidate.Describe(categoryKey: $"category-{index % 20}");
            candidate.Place(countryCode);
            candidate.Rate((decimal)(index % 100) / 100m, 0.5m, index);
            candidate.Restrict(CandidateVisibility.Public);
            candidate.Schedule(clock.UtcNow.AddDays(-(index % 90)), clock.UtcNow.AddDays(-(index % 90)));

            repository.Update(candidate);

            // Saved in batches so the change tracker does not hold two thousand aggregates and their
            // owned rows at once, which on SQLite is the difference between a slow test and a hung
            // one.
            if (index % 200 == 199)
            {
                await unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
        }

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SeedProfileAsync(UserId userId, string countryCode)
    {
        using var scope = Factory.Services.CreateScope();

        var store = scope.ServiceProvider.GetRequiredService<ISignalStore>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await store.RecordAsync(
            new SignalDraft
            {
                UserId = userId,
                Kind = SignalKind.Location,
                Source = SignalSource.Identity,
                Context = SignalContext.Create(countryCode),
                SourceEventName = "test"
            },
            CancellationToken.None);

        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        var builder = scope.ServiceProvider.GetRequiredService<IPersonalizationProfileBuilder>();

        await builder.BuildIncrementalAsync(userId, CancellationToken.None);

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SeedSignalsAsync(UserId userId, int count)
    {
        using var scope = Factory.Services.CreateScope();

        var store = scope.ServiceProvider.GetRequiredService<ISignalStore>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var drafts = Enumerable.Range(0, count)
            .Select(index => new SignalDraft
            {
                UserId = userId,
                Kind = SignalKind.Viewed,
                Source = SignalSource.Client,
                TargetType = RecommendationTargetType.Guide,
                TargetId = Guid.NewGuid(),
                Key = $"category-{index % 300}",
                KeyKind = InterestKind.Category,
                Context = SignalContext.Create("MU"),
                SourceEventName = "test"
            })
            .ToList();

        // In chunks, for the same change-tracker reason the candidate seed batches.
        foreach (var chunk in drafts.Chunk(500))
        {
            await store.RecordManyAsync(chunk, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task SeedDismissalsAsync(UserId userId, int count)
    {
        using var scope = Factory.Services.CreateScope();

        var feedback = scope.ServiceProvider
            .GetRequiredService<IRecommendationFeedbackRepository>();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

        for (var index = 0; index < count; index++)
        {
            feedback.Add(RecommendationFeedback.Record(
                RecommendationFeedbackId.From(
                    guids.NewSequentialGuid()),
                userId,
                TargetReference.Create(RecommendationTargetType.Guide, Guid.NewGuid()),
                RecommendationFeedbackKind.Dismissed,
                RecommendationSurface.Guides,
                clock.UtcNow,
                suppressedUntil: clock.UtcNow.AddDays(90)));

            if (index % 250 == 249)
            {
                await unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
        }

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }
}
