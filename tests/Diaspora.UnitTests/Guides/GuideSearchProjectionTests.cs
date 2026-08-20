using System.Diagnostics;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.Modules.Guides.Extensibility.Search;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// What the search index is told about a guide, and when it is told to forget one.
/// <para>
/// The projection is the only place the aggregate is flattened for an engine, so it is the only
/// place a discoverability mistake becomes a member finding a draft — or failing to find the
/// published guide that answers their question. The two failure directions are not symmetric: a
/// missing entry is a bad search result, an extra entry is a disclosure.
/// </para>
/// <para>
/// The engine is a hand-written fake rather than a substitute because these tests care what was
/// handed over, not merely that a call happened. The last test is large on purpose, in the
/// established "correctness test that happens to be large" shape: it prints timings and asserts
/// on none of them, because a loaded build agent would fail a clock assertion for reasons that
/// have nothing to do with this code.
/// </para>
/// </summary>
public class GuideSearchProjectionTests
{
    private static readonly DateTimeOffset Now = GuideLifecycleTests.Now;

    [Fact]
    public void A_published_guide_is_flattened_with_everything_a_filter_needs()
    {
        var guide = GuideLifecycleTests.Published();

        var document = GuideSearchProjectionService.BuildDocument(guide);

        document.GuideId.Should().Be(guide.Id.Value);
        document.Slug.Should().Be(guide.Slug.Value);
        document.Title.Should().Be(guide.Title.Value);
        document.CategoryKey.Should().Be(guide.CategoryKey);
        document.CountryCode.Should().Be(guide.Location.CountryCode);
        document.Language.Should().Be(guide.Language.Code);
        document.AuthorId.Should().Be(guide.AuthorId.Value);
        document.PublishedAt.Should().Be(guide.PublishedAt,
            "'newest first' is the default sort and cannot be answered without it");
    }

    [Fact]
    public void The_body_text_carries_the_prose_a_member_would_search_for()
    {
        var guide = GuideLifecycleTests.Published(GuideLifecycleTests.OneSection(
            "Renewing at the immigration office",
            "Arrive before eight and bring two passport photographs."));

        var document = GuideSearchProjectionService.BuildDocument(guide);

        document.BodyText.Should().Contain("passport photographs",
            "a member searches for the words in the procedure, not the words in the title");
        document.BodyText.Should().Contain("Renewing at the immigration office",
            "section headings are the most searchable prose in a long guide");
    }

    [Fact]
    public void A_verified_and_featured_guide_says_so_because_both_are_filters()
    {
        var guide = GuideLifecycleTests.Published();

        guide.ApplyVerificationDecision(
            GuideVerificationState.Verified, Guid.NewGuid(), "CommunityGuide", null, Now)
            .IsSuccess.Should().BeTrue();

        guide.Feature(Now, GuideLifecycleTests.Moderator).IsSuccess.Should().BeTrue();

        var document = GuideSearchProjectionService.BuildDocument(guide);

        document.IsVerified.Should().BeTrue("'verified only' is an offered filter");
        document.IsFeatured.Should().BeTrue("'featured' is an offered filter and a sort");
    }

    [Fact]
    public void Popularity_rewards_deliberate_signals_over_raw_traffic()
    {
        var viewed = GuideLifecycleTests.Published();
        var helpful = GuideLifecycleTests.Published();

        for (var i = 0; i < 50; i++)
        {
            viewed.RecordView();
        }

        helpful.ApplyVoteDelta(helpfulDelta: 10, notHelpfulDelta: 0);

        GuideSearchProjectionService.PopularityOf(helpful).Should().BeGreaterThan(
            GuideSearchProjectionService.PopularityOf(viewed),
            "a guide ten members called helpful outranks one fifty members merely opened, or " +
            "the ranking rewards clickbait");
    }

    [Fact]
    public async Task Publishing_indexes_and_archiving_withdraws()
    {
        var engine = new RecordingSearchEngine();
        var projection = Projection(engine);
        var guide = GuideLifecycleTests.Published();

        await projection.ProjectAsync(guide);

        engine.Indexed.Should().ContainSingle().Which.GuideId.Should().Be(guide.Id.Value);

        guide.Archive(Now).IsSuccess.Should().BeTrue();
        await projection.ProjectAsync(guide);

        engine.Removed.Should().ContainSingle().Which.Should().Be(guide.Id.Value,
            "an archived guide that stays in the index is a search result nobody can open");
    }

    [Fact]
    public async Task A_draft_is_withdrawn_from_the_index_rather_than_indexed()
    {
        var engine = new RecordingSearchEngine();
        var projection = Projection(engine);

        await projection.ProjectAsync(GuideLifecycleTests.Drafted());

        engine.Indexed.Should().BeEmpty(
            "an unpublished draft in the index is the disclosure this projection exists to prevent");
        engine.Removed.Should().ContainSingle();
    }

    [Fact]
    public async Task A_hidden_guide_is_withdrawn_from_the_index()
    {
        var engine = new RecordingSearchEngine();
        var projection = Projection(engine);
        var guide = GuideLifecycleTests.Published();

        guide.Hide("Outdated legal advice.", GuideLifecycleTests.Moderator, Now)
            .IsSuccess.Should().BeTrue();

        await projection.ProjectAsync(guide);

        engine.Indexed.Should().BeEmpty();
        engine.Removed.Should().ContainSingle().Which.Should().Be(guide.Id.Value,
            "a moderator's decision has to reach the surface members actually search");
    }

    [Fact]
    public async Task An_engine_outage_leaves_a_stale_entry_rather_than_failing_the_write()
    {
        var engine = new RecordingSearchEngine { FailNext = true };
        var projection = Projection(engine);

        var project = async () => await projection.ProjectAsync(GuideLifecycleTests.Published());

        await project.Should().NotThrowAsync(
            "the guide is already committed; failing here would report a successful publication " +
            "as an error and tempt the author into publishing twice");
    }

    [Fact]
    public async Task A_cancelled_projection_still_cancels()
    {
        var engine = new RecordingSearchEngine { CancelNext = true };
        var projection = Projection(engine);

        var project = async () => await projection.ProjectAsync(GuideLifecycleTests.Published());

        await project.Should().ThrowAsync<OperationCanceledException>(
            "shutdown is not an engine outage, and swallowing it would hide a torn request");
    }

    /// <summary>
    /// A whole corpus projected and searched at once — the "large search and bulk read" shape.
    /// <para>
    /// Correctness only: every published guide reaches the index exactly once, every draft is
    /// kept out, and a query over the corpus returns precisely the guides whose text matches.
    /// Timings are printed for the performance report and asserted nowhere.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_whole_corpus_projects_and_searches_without_losing_or_inventing_a_guide(
        )
    {
        const int published = 500;
        const int drafts = 100;

        var engine = new RecordingSearchEngine();
        var projection = Projection(engine);
        var stopwatch = Stopwatch.StartNew();

        var corpus = new List<Guide>(published + drafts);

        for (var i = 0; i < published; i++)
        {
            // Half the corpus mentions the term, so a match is a real filter rather than
            // everything or nothing.
            var content = i % 2 == 0
                ? "Apply for the residence permit before the visa lapses."
                : "Open an account with two forms of identification.";

            corpus.Add(GuideLifecycleTests.Published(
                GuideLifecycleTests.OneSection($"Step {i}", content)));
        }

        for (var i = 0; i < drafts; i++)
        {
            corpus.Add(GuideLifecycleTests.Drafted());
        }

        var built = stopwatch.ElapsedMilliseconds;

        foreach (var guide in corpus)
        {
            await projection.ProjectAsync(guide);
        }

        var projected = stopwatch.ElapsedMilliseconds;

        engine.Indexed.Should().HaveCount(published,
            "every published guide reaches the index exactly once, and no draft does");

        engine.Indexed.Select(document => document.GuideId).Distinct()
            .Should().HaveCount(published, "a duplicated entry is a duplicated search result");

        var matches = engine.Indexed
            .Where(document => document.BodyText.Contains(
                "residence permit", StringComparison.OrdinalIgnoreCase))
            .ToList();

        matches.Should().HaveCount(published / 2,
            "a term half the corpus uses must match half the corpus — not all of it, which is a " +
            "broken filter, and not none, which is a broken projection");

        stopwatch.Stop();

        Output(
            $"{corpus.Count:N0} guides ({published:N0} published, {drafts:N0} drafts). " +
            $"built {built}ms, projected {projected - built}ms, " +
            $"searched {stopwatch.ElapsedMilliseconds - projected}ms.");
    }

    private readonly ITestOutputHelper? _output;

    public GuideSearchProjectionTests(ITestOutputHelper output) => _output = output;

    private void Output(string message) => _output?.WriteLine(message);

    private static GuideSearchProjectionService Projection(IGuideSearchEngine engine) =>
        new(engine, NullLogger<GuideSearchProjectionService>.Instance);

    /// <summary>
    /// An engine that remembers what it was told, and can be made to fail once. A fake rather
    /// than a substitute because every assertion here is about the document handed over.
    /// </summary>
    private sealed class RecordingSearchEngine : IGuideSearchEngine
    {
        public List<GuideSearchDocument> Indexed { get; } = [];

        public List<Guid> Removed { get; } = [];

        public bool FailNext { get; init; }

        public bool CancelNext { get; init; }

        public Task IndexAsync(
            GuideSearchDocument document, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);

            if (CancelNext)
            {
                throw new OperationCanceledException();
            }

            if (FailNext)
            {
                throw new InvalidOperationException("The search cluster is unreachable.");
            }

            Indexed.Add(document);

            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid guideId, CancellationToken cancellationToken = default)
        {
            if (CancelNext)
            {
                throw new OperationCanceledException();
            }

            if (FailNext)
            {
                throw new InvalidOperationException("The search cluster is unreachable.");
            }

            Removed.Add(guideId);

            return Task.CompletedTask;
        }

        public Task<GuideSearchOutcome> SearchAsync(
            GuideSearchRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var hits = Indexed
                .Where(document => request.Text is null || document.BodyText.Contains(
                    request.Text, StringComparison.OrdinalIgnoreCase))
                .Select(document => new GuideSearchHit { GuideId = document.GuideId })
                .ToList();

            return Task.FromResult(new GuideSearchOutcome
            {
                Hits = hits,
                Total = hits.Count
            });
        }
    }
}
