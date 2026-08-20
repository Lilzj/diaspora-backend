using System.Diagnostics;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Saved;

using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Volume, and what breaks at it.
/// <para>
/// <b>These are correctness tests that happen to be large.</b> The timings they print go in the
/// performance report; none of them asserts on wall-clock time, because a loaded build agent
/// would fail a timing assertion for reasons that have nothing to do with the code, and a suite
/// that goes red for that stops being read.
/// </para>
/// <para>
/// What they do assert is what only shows up at volume: a cursor that walks a large result set
/// without losing or repeating a row, a filter that still filters when there are thousands of
/// rows to filter, an applicant list that pages cleanly under a flood, and counters that stay
/// true after thousands of writes. Each of those is silently correct on ten rows.
/// </para>
/// </summary>
public sealed class JobsScaleTests : IntegrationTestBase
{
    /// <summary>
    /// Large enough that a table scan and an index lookup diverge, and that a cursor bug shows
    /// up as a missing or repeated row rather than passing by luck. Small enough that the whole
    /// suite still finishes in a coffee break.
    /// </summary>
    private const int PostingCount = 2_000;

    private const int ApplicationCount = 1_000;

    private readonly ITestOutputHelper _output;

    public JobsScaleTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory) => _output = output;

    // ── Search over a large corpus ──────────────────────────────────────────────────────────

    /// <summary>
    /// The whole corpus walked page by page. Every posting is seen exactly once — which is the
    /// assertion a cursor exists to earn, and the one that quietly fails when two rows share a
    /// sort key and the tie-break is missing.
    /// </summary>
    [Fact]
    public async Task The_cursor_walks_the_whole_corpus_without_loss_or_repetition()
    {
        var employer = await SeedPostingsAsync(PostingCount);

        using var scope = Factory.Services.CreateScope();
        var postings = scope.ServiceProvider.GetRequiredService<IJobPostingRepository>();

        var seen = new HashSet<Guid>();
        var pages = 0;
        string? cursor = null;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var page = await postings.QueryAsync(
                new JobQueryFilter { EmployerId = employer, Statuses = [JobStatus.Open] },
                cursor,
                limit: 50);

            foreach (var posting in page.Items)
            {
                seen.Add(posting.Id.Value).Should().BeTrue(
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

        seen.Should().HaveCount(PostingCount, "every posting is seen exactly once");

        _output.WriteLine(
            $"Walked {PostingCount} postings in {pages} pages of 50 " +
            $"in {stopwatch.ElapsedMilliseconds} ms " +
            $"({stopwatch.ElapsedMilliseconds / (double)pages:F1} ms/page).");
    }

    /// <summary>
    /// A page is a page. A limit that leaks under volume is how a mobile client on a metered
    /// connection ends up downloading two thousand job cards.
    /// </summary>
    [Fact]
    public async Task A_page_stays_bounded_however_much_matches()
    {
        var employer = await SeedPostingsAsync(PostingCount);

        using var scope = Factory.Services.CreateScope();
        var postings = scope.ServiceProvider.GetRequiredService<IJobPostingRepository>();

        var page = await postings.QueryAsync(
            new JobQueryFilter { EmployerId = employer }, cursor: null, limit: 20);

        page.Items.Should().HaveCount(20);
        page.HasMore.Should().BeTrue();
    }

    /// <summary>
    /// The filter that matters most to this audience, asserted against a corpus large enough
    /// that "returns some rows" is not evidence it ran. Every row back must actually sponsor.
    /// </summary>
    [Fact]
    public async Task The_sponsorship_filter_still_filters_at_volume()
    {
        var employer = await SeedPostingsAsync(PostingCount);

        using var scope = Factory.Services.CreateScope();
        var postings = scope.ServiceProvider.GetRequiredService<IJobPostingRepository>();

        var stopwatch = Stopwatch.StartNew();

        var page = await postings.QueryAsync(
            new JobQueryFilter { EmployerId = employer, VisaSponsorshipOnly = true },
            cursor: null,
            limit: 50);

        stopwatch.Stop();

        page.Items.Should().NotBeEmpty("a third of the corpus sponsors");

        page.Items.Should().OnlyContain(
            posting => posting.WorkArrangement.OffersVisaSponsorship,
            "a sponsorship filter that returns non-sponsoring roles wastes an application");

        _output.WriteLine(
            $"Sponsorship-filtered page of 50 from {PostingCount} " +
            $"in {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// Country and category together, the commonest browse in the product. Both must hold, not
    /// whichever the query builder applied last.
    /// </summary>
    [Fact]
    public async Task Combined_filters_all_hold_at_volume()
    {
        var employer = await SeedPostingsAsync(PostingCount);

        using var scope = Factory.Services.CreateScope();
        var postings = scope.ServiceProvider.GetRequiredService<IJobPostingRepository>();

        var page = await postings.QueryAsync(
            new JobQueryFilter
            {
                EmployerId = employer,
                CountryCode = "DE",
                CategoryKey = "healthcare",
                Statuses = [JobStatus.Open]
            },
            cursor: null,
            limit: 50);

        page.Items.Should().NotBeEmpty();

        page.Items.Should().OnlyContain(posting =>
            posting.Location.CountryCode == "DE"
            && posting.CategoryKey == "healthcare"
            && posting.Status == JobStatus.Open);
    }

    /// <summary>
    /// A filter matching nothing returns an empty page and reports exhaustion, rather than a
    /// cursor the client will follow forever.
    /// </summary>
    [Fact]
    public async Task An_unmatched_filter_terminates_rather_than_looping()
    {
        var employer = await SeedPostingsAsync(200);

        using var scope = Factory.Services.CreateScope();
        var postings = scope.ServiceProvider.GetRequiredService<IJobPostingRepository>();

        var page = await postings.QueryAsync(
            new JobQueryFilter { EmployerId = employer, CountryCode = "AQ" },
            cursor: null,
            limit: 50);

        page.Items.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
    }

    // ── A flood of applications on one posting ──────────────────────────────────────────────

    /// <summary>
    /// One posting, a thousand applicants — a real shape for a sponsored role, and the case where
    /// an unindexed applicant list turns a recruiter's first screen into a timeout.
    /// </summary>
    [Fact]
    public async Task A_thousand_applications_page_cleanly_and_count_true()
    {
        var (posting, employer) = await SeedPostingWithApplicationsAsync(ApplicationCount);

        using var scope = Factory.Services.CreateScope();
        var applications = scope.ServiceProvider
            .GetRequiredService<IJobApplicationRepository>();

        var seen = new HashSet<Guid>();
        string? cursor = null;
        var pages = 0;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var page = await applications.QueryAsync(
                new ApplicationQueryFilter { EmployerId = employer, PostingId = posting },
                cursor,
                limit: 50);

            foreach (var application in page.Items)
            {
                seen.Add(application.Id.Value).Should().BeTrue(
                    "an applicant listed twice is a recruiter reviewing a ghost");
            }

            pages++;

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            cursor = page.NextCursor;

            pages.Should().BeLessThan(100);
        }

        stopwatch.Stop();

        seen.Should().HaveCount(ApplicationCount);

        _output.WriteLine(
            $"Paged {ApplicationCount} applications in {pages} pages " +
            $"in {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// The applicant counter on the posting is what the employer's dashboard shows and what the
    /// candidate weighs before spending an evening on an application. After a thousand writes it
    /// must still equal the truth.
    /// </summary>
    [Fact]
    public async Task The_applicant_counter_survives_a_thousand_writes()
    {
        var (postingId, _) = await SeedPostingWithApplicationsAsync(ApplicationCount);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var actual = await dbContext.Set<JobApplication>()
            .CountAsync(application => application.PostingId == postingId);

        var posting = await dbContext.Set<JobPosting>()
            .FirstAsync(candidate => candidate.Id == postingId);

        posting.Engagement.ApplicationCount.Should().Be(
            actual, "a counter that drifts is worse than no counter at all");
    }

    /// <summary>
    /// The degenerate case, and not a contrived one: <c>CreatedAt</c> is stamped by the shared
    /// persistence interceptor, so every row written in one unit of work carries the same
    /// instant. Three hundred saved jobs imported together therefore share a single timestamp —
    /// one tie group twelve pages wide.
    /// <para>
    /// A cursor comparing timestamps alone returns nothing after page one. A cursor carrying
    /// only the last page's identifiers hands page one's rows back on page three. Only one that
    /// accumulates while the boundary holds still walks the list.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_saved_list_pages_cleanly_when_every_row_shares_one_timestamp()
    {
        const int SavedCount = 300;

        Factory.EnsureDatabaseCreated();

        var candidateId = CandidateProfileId.New();
        var userId = UserId.New();
        var now = DateTimeOffset.UtcNow;
        var run = Guid.NewGuid().ToString("N")[..8];

        using (var seed = Factory.Services.CreateScope())
        {
            var dbContext = seed.ServiceProvider.GetRequiredService<DiasporaDbContext>();

            var saved = new List<SavedJob>(SavedCount);

            for (var index = 0; index < SavedCount; index++)
            {
                saved.Add(SavedJob.Create(
                    SavedJobId.New(),
                    candidateId,
                    userId,
                    JobPostingId.New(),
                    note: $"saved-{run}-{index}",
                    // Written here, overwritten by the persistence interceptor at SaveChanges —
                    // which is the whole point: all three hundred end up on one instant.
                    now.AddMinutes(-index),
                    remindBeforeClosing: false).Value);
            }

            dbContext.Set<SavedJob>().AddRange(saved);

            await dbContext.SaveChangesAsync();
        }

        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISavedJobRepository>();

        var seen = new HashSet<Guid>();
        string? cursor = null;
        var pages = 0;

        while (true)
        {
            var page = await repository.GetForCandidateAsync(candidateId, cursor, limit: 25);

            foreach (var entry in page.Items)
            {
                seen.Add(entry.Id.Value).Should().BeTrue();
            }

            pages++;

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            cursor = page.NextCursor;

            pages.Should().BeLessThan(60);
        }

        seen.Should().HaveCount(SavedCount);
    }

    /// <summary>
    /// <b>The concurrency guard is a unique index, not a check-then-write.</b> Two taps racing on
    /// a flaky connection both read "no application yet"; only the database can settle it. This
    /// asserts the constraint is really in the schema rather than only in the handler — the
    /// handler's check would pass both and the second insert must still fail.
    /// </summary>
    [Fact]
    public async Task A_duplicate_application_is_refused_by_the_database_itself()
    {
        var (postingId, _) = await SeedPostingWithApplicationsAsync(count: 1);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var existing = await dbContext.Set<JobApplication>()
            .AsNoTracking()
            .FirstAsync(application => application.PostingId == postingId);

        // The same candidate, the same posting, bypassing the handler entirely.
        var duplicate = JobApplication.Submit(
            JobApplicationId.New(),
            postingId,
            existing.EmployerId,
            existing.CandidateId,
            existing.ApplicantUserId,
            resumeId: null,
            coverLetter: null,
            answers: [],
            attachments: [],
            DateTimeOffset.UtcNow).Value;

        dbContext.Set<JobApplication>().Add(duplicate);

        var write = async () => await dbContext.SaveChangesAsync();

        await write.Should().ThrowAsync<DbUpdateException>(
            "the unique index on (candidate, posting) is the only thing that can settle a race");
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds directly through the context. Driving HTTP for two thousand postings would test the
    /// registration flow rather than the read paths, and take long enough that nobody runs it.
    /// <para>
    /// Returns its own employer identifier, and every test filters by it. The whole class shares
    /// one database, so a test that asserted on a global count would pass or fail depending on
    /// which other tests had run first — which is a worse failure than the bug it was meant to
    /// catch, because it is not reproducible.
    /// </para>
    /// </summary>
    private async Task<EmployerProfileId> SeedPostingsAsync(int count)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var employerId = EmployerProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        // Slugs are unique platform-wide; two seeding runs in one database would collide.
        var run = Guid.NewGuid().ToString("N")[..8];

        var countries = new[] { "DE", "GB", "NG", "GH", "KE" };
        var categories = new[] { "healthcare", "construction", "technology", "hospitality" };

        var postings = new List<JobPosting>(count);

        for (var index = 0; index < count; index++)
        {
            var posting = JobPosting.Create(
                JobPostingId.New(),
                employerId,
                ownerId,
                JobTitle.Create($"Seeded Role {index}").Value,
                JobSlug.Create($"seeded-role-{run}-{index}").Value,
                JobDescription.Create(
                    $"Seeded posting {index}, long enough to be a valid description.").Value,
                categories[index % categories.Length],
                JobType.FullTime,
                ExperienceLevel.Mid,
                JobLocationContext.Create(countries[index % countries.Length]).Value,
                WorkArrangement.Create(WorkplaceType.OnSite, index % 3 == 0).Value,
                // Spread over time so the cursor has real ordering to do. Two share a timestamp
                // every so often, which is exactly where a missing tie-break shows up.
                now.AddMinutes(-(index / 2))).Value;

            posting.Publish(now, openImmediately: true);

            postings.Add(posting);
        }

        dbContext.Set<JobPosting>().AddRange(postings);

        await dbContext.SaveChangesAsync();

        return employerId;
    }

    /// <summary>
    /// One published posting with <paramref name="count"/> applications against it. Returns the
    /// employer too: the repository refuses an applicant listing that is not scoped to an
    /// employer, a candidate or staff, because a posting-only listing would hand back other
    /// people's applications.
    /// </summary>
    private async Task<(JobPostingId Posting, EmployerProfileId Employer)>
        SeedPostingWithApplicationsAsync(int count)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var employerId = EmployerProfileId.New();
        var now = DateTimeOffset.UtcNow;

        var posting = JobPosting.Create(
            JobPostingId.New(),
            employerId,
            UserId.New(),
            JobTitle.Create("Heavily Applied Role").Value,
            JobSlug.Create($"heavily-applied-{Guid.NewGuid():N}").Value,
            JobDescription.Create("A sponsored role that attracts a very large field.").Value,
            "healthcare",
            JobType.FullTime,
            ExperienceLevel.Mid,
            JobLocationContext.Create("DE", cityName: "Berlin").Value,
            WorkArrangement.Create(WorkplaceType.OnSite, offersVisaSponsorship: true).Value,
            now.AddDays(-30)).Value;

        posting.Publish(now.AddDays(-30), openImmediately: true);

        var applications = new List<JobApplication>(count);

        for (var index = 0; index < count; index++)
        {
            applications.Add(JobApplication.Submit(
                JobApplicationId.New(),
                posting.Id,
                employerId,
                CandidateProfileId.New(),
                UserId.New(),
                resumeId: null,
                coverLetter: null,
                answers: [],
                attachments: [],
                // Groups of three sharing a timestamp. Deliberately not a divisor of the page
                // size: with groups of two and pages of fifty, every boundary falls neatly
                // between groups and a cursor with no tie-break passes by luck.
                now.AddMinutes(-(index / 3))).Value);

            // Kept in step with the rows, which is what the later assertion checks.
            posting.ApplyApplicationDelta(1);
        }

        dbContext.Set<JobPosting>().Add(posting);
        dbContext.Set<JobApplication>().AddRange(applications);

        await dbContext.SaveChangesAsync();

        return (posting.Id, employerId);
    }
}
