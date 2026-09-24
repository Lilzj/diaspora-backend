using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The two filters <c>GET /jobs</c> binds, actually narrowing — 20 Sep 2026.
/// <para>
/// <b>Written because the client started sending them and nothing had ever proved they worked.</b>
/// Discover's Jobs sheet set an employment type and a verified-employer toggle that no request
/// carried; wiring them up meant reading <c>JobPostingRepository</c>, seeing both applied, and
/// believing it. That is the same evidence that failed on the creator keyset the same day — a
/// filter that reads correctly and does nothing looks exactly like one that works, and production
/// has no published jobs to tell the difference on.
/// </para>
/// <para>
/// <b>The hyphen case is the point of the last test.</b> The route parses <c>type</c> with
/// <c>Enum.TryParse(..., ignoreCase: true)</c>, and an unparseable value does not fail the request
/// — <c>ParseMany</c> drops it and the filter disappears. So sending the chip's own label,
/// <c>Full-Time</c>, returns everything while looking like a filter.
/// </para>
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class JobBrowseFilterTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/jobs";

    public JobBrowseFilterTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task The_employment_type_narrows_to_that_type()
    {
        var run = await SeedAsync();
        Api.ClearAuthentication();

        var full = await TitlesAsync($"?country=MU&type=FullTime&q={run}");
        var volunteer = await TitlesAsync($"?country=MU&type=Volunteer&q={run}");

        // Both full-time postings, across both employers: the type filter is about the posting
        // and says nothing about who placed it. Expecting one here is the mistake that wrote this
        // comment, and it would have hidden the two filters being wired to each other.
        full.Should().BeEquivalentTo([$"full time {run}", $"unverified full time {run}"]);
        volunteer.Should().BeEquivalentTo([$"volunteer {run}"]);
    }

    [Fact]
    public async Task The_verified_filter_narrows_to_verified_employers()
    {
        var run = await SeedAsync();
        Api.ClearAuthentication();

        var verified = await TitlesAsync($"?country=MU&verified=true&q={run}");

        verified.Should().BeEquivalentTo(
            [$"full time {run}", $"volunteer {run}"],
            "both of those belong to the employer Trust vouched for");
    }

    [Fact]
    public async Task Both_filters_narrow_together()
    {
        var run = await SeedAsync();
        Api.ClearAuthentication();

        var both = await TitlesAsync($"?country=MU&type=FullTime&verified=true&q={run}");

        both.Should().BeEquivalentTo([$"full time {run}"]);
    }

    [Fact]
    public async Task Asking_for_nothing_in_particular_still_answers_everything()
    {
        var run = await SeedAsync();
        Api.ClearAuthentication();

        var all = await TitlesAsync($"?country=MU&q={run}");

        all.Should().HaveCount(3, "otherwise the narrowing tests prove nothing");
    }

    /// <summary>
    /// <b>An unparseable type is not refused, it is dropped.</b> Which makes this the shape the
    /// client must never send: it looks like a filter and returns the unfiltered set.
    /// </summary>
    [Fact]
    public async Task A_type_the_route_cannot_parse_filters_nothing()
    {
        var run = await SeedAsync();
        Api.ClearAuthentication();

        var hyphenated = await TitlesAsync($"?country=MU&type=Full-Time&q={run}");

        hyphenated.Should().HaveCount(
            3, "an unparseable type widens the search rather than failing it");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<List<string>> TitlesAsync(string query)
    {
        var page = await IdentityApiClient.ReadDataAsync<PageShape>(
            await HttpClient.GetAsync($"{Root}{query}&limit=50"));

        return [.. page.Items.Select(item => item.Title)];
    }

    /// <summary>
    /// Three postings: two full-time and one volunteer, split across a verified employer and an
    /// unverified one, so neither filter can pass by accident.
    /// </summary>
    private async Task<string> SeedAsync()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var vouchedFor = Employer(dbContext, $"Vouched {run}", $"vouched-{run}", now, verified: true);
        var unknown = Employer(dbContext, $"Unknown {run}", $"unknown-{run}", now, verified: false);

        dbContext.Set<JobPosting>().AddRange(
            Posting(vouchedFor, $"full time {run}", JobType.FullTime, now),
            Posting(vouchedFor, $"volunteer {run}", JobType.Volunteer, now),
            Posting(unknown, $"unverified full time {run}", JobType.FullTime, now));

        await dbContext.SaveChangesAsync();

        return run;
    }

    private static EmployerProfileId Employer(
        DiasporaDbContext dbContext, string name, string slug, DateTimeOffset now, bool verified)
    {
        var id = EmployerProfileId.New();

        var employer = EmployerProfile.Create(
            id,
            UserId.New(),
            CompanyIdentity.Create(
                name, null, null, EmployerOrganizationType.Company, CompanySize.Unspecified, null).Value,
            JobSlug.Create(slug).Value,
            "MU",
            now).Value;

        if (verified)
        {
            // Through the decision the Trust consumer applies, not by writing the badge: the
            // filter correlates against `TrustMark.State` and `Status`, and a test that set the
            // field directly would not notice if either rule moved.
            employer.ApplyVerificationDecision(
                JobVerificationState.Verified, Guid.NewGuid(), badge: "verified", reason: null,
                decidedAt: now).IsSuccess.Should().BeTrue();
        }

        dbContext.Set<EmployerProfile>().Add(employer);

        return id;
    }

    private static JobPosting Posting(
        EmployerProfileId employerId, string title, JobType type, DateTimeOffset now)
    {
        var posting = JobPosting.Create(
            JobPostingId.New(),
            employerId,
            UserId.New(),
            JobTitle.Create(title).Value,
            JobSlug.Create(title.Replace(' ', '-')).Value,
            JobDescription.Create(
                "A posting seeded for the browse filter tests, long enough to be valid.").Value,
            "hospitality",
            type,
            ExperienceLevel.Mid,
            JobLocationContext.Create("MU", cityName: "Rose Hill").Value,
            WorkArrangement.Create(WorkplaceType.OnSite, offersVisaSponsorship: false).Value,
            now).Value;

        posting.Publish(now, openImmediately: true);

        return posting;
    }

    private sealed record PageShape
    {
        public List<ItemShape> Items { get; init; } = [];
    }

    private sealed record ItemShape
    {
        public string Title { get; init; } = string.Empty;
    }
}
