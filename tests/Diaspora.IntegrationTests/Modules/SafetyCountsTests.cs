using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Safety as sourced counts, never a score — 17 Sep 2026 (FRONTEND-MAPS-GAP-015).
/// <para>
/// The user decided the platform publishes what named sources counted instead of a safety score:
/// corroborated community safety reports everywhere, and police-recorded crime where an official
/// open source covers the country, separately, each with its source, period and method. The seed
/// sets up what must be counted and every near miss that must not be: an unverified report, a
/// withdrawn one, one outside the window, one in another category, one in another place.
/// </para>
/// <para>
/// The police source is disabled in this host — the suite never calls a third party — so it must be
/// published as unavailable, never as zero. Its parsing is covered by <c>PoliceUkCrimeDataTests</c>.
/// </para>
/// </summary>
public sealed class SafetyCountsTests : IdentityTestBase
{
    private const string Root = "/api/v1/geo";

    public SafetyCountsTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_neighbourhood_gets_its_corroborated_community_reports_and_still_no_score()
    {
        var peckham = await PathOfAsync("Peckham");
        var brixton = await PathOfAsync("Brixton");
        var now = DateTimeOffset.UtcNow;

        await SeedReportAsync(peckham, PostCategory.Police, now.AddDays(-3), corroborated: true);
        await SeedReportAsync(peckham, PostCategory.Police, now.AddDays(-40), corroborated: true);
        await SeedReportAsync(peckham, PostCategory.Emergency, now.AddDays(-10), corroborated: true);

        // Not counted, each for its own reason.
        await SeedReportAsync(peckham, PostCategory.Police, now.AddDays(-5), corroborated: false);
        await SeedReportAsync(peckham, PostCategory.Police, now.AddDays(-6), corroborated: true, withdraw: true);
        await SeedReportAsync(peckham, PostCategory.Police, now.AddDays(-120), corroborated: true);
        await SeedReportAsync(peckham, PostCategory.Traffic, now.AddDays(-2), corroborated: true);
        await SeedReportAsync(brixton, PostCategory.Police, now.AddDays(-2), corroborated: true);

        await SignInAsync();

        var safety = await IdentityApiClient.ReadDataAsync<SafetyShape>(
            await HttpClient.GetAsync($"{Root}/neighborhoods/peckham/safety"));

        safety.IsAvailable.Should().BeFalse("counts are published, a score is not");
        safety.Score.Should().BeNull();

        var community = safety.Counts.Single(count => count.Kind == "community-reports");
        community.IsAvailable.Should().BeTrue();
        community.Total.Should().Be(3);
        community.Categories.Should().BeEquivalentTo(new[]
        {
            new CategoryShape { Id = "Police", Label = "Police", Count = 2 },
            new CategoryShape { Id = "Emergency", Label = "Emergency", Count = 1 }
        }, options => options.WithStrictOrdering());
        community.Source.Should().Be("Diaspora community reports");
        community.Method.Should().Contain("90 days");
        community.From.Should().BeCloseTo(now.AddDays(-90), TimeSpan.FromMinutes(5));

        // Peckham is in England: the police source applies, and here it is not read.
        var police = safety.Counts.Single(count => count.Kind == "police-recorded-crime");
        police.IsAvailable.Should().BeFalse();
        police.UnavailableReason.Should().Be("Disabled");
        police.Total.Should().BeNull("a source that was not read is not a count of zero");
        police.Attribution.Should().Contain("Open Government Licence");

        safety.Limitations.Should().Contain(line => line.StartsWith("Diaspora does not publish a safety score"));
    }

    /// <summary>
    /// <b>A place no source covers says so</b> — decided 17 Sep 2026, after the section was simply
    /// left out. "Nothing here" read as "Diaspora did not look", "the police recorded nothing" and
    /// "this place is safe" all at once, and the member had no way to tell which.
    /// </summary>
    [Fact]
    public async Task A_place_no_police_source_covers_is_told_nobody_publishes_the_figure()
    {
        await SignInAsync();

        var safety = await IdentityApiClient.ReadDataAsync<SafetyShape>(
            await HttpClient.GetAsync($"{Root}/neighborhoods/rose-hill-central/safety"));

        safety.Counts.Select(count => count.Kind)
            .Should().Equal("community-reports", "police-recorded-crime");

        safety.Counts[0].Total.Should().Be(0);

        var police = safety.Counts[1];
        police.IsAvailable.Should().BeFalse();
        police.UnavailableReason.Should().Be("NotPublished");
        police.Total.Should().BeNull("no source is not a count of zero");
        police.Area.Should().Be("Places in Mauritius");
        police.Method.Should().Contain("Mauritius");

        // data.police.uk's licence belongs to data.police.uk, and there is no data here to credit.
        police.Attribution.Should().BeNull();
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────────────────────────

    private async Task SignInAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private async Task<string> PathOfAsync(string neighbourhood)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();

        var areas = await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<AdministrativeArea>().AsNoTracking()
            .Where(area => area.Level == AdministrativeLevel.Neighborhood)
            .ToListAsync();

        return areas.Single(area => area.Name.Value == neighbourhood).Path.Value;
    }

    private async Task SeedReportAsync(
        string path, PostCategory category, DateTimeOffset publishedAt, bool corroborated, bool withdraw = false)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var author = UserId.New();

        var post = CommunityPost.Draft(
            author,
            PublishingIdentity.Personal(),
            PostBody.Create($"A seeded safety report for the counts suite, {Guid.NewGuid():N}.").Value,
            null,
            category,
            UrgencyLevel.Normal,
            ContentAudience.Public,
            PostLocationContext.Create(countryCode: path.Split('/')[1].ToUpperInvariant(), hierarchyPath: path),
            publishedAt).Value;

        post.Publish(publishedAt).IsSuccess.Should().BeTrue();

        if (corroborated)
        {
            var (total, verified) = ConfirmationThresholds.For(UrgencyLevel.Normal);

            for (var index = 0; index < total; index++)
            {
                post.RecordCorroboration(index < verified, 1);
            }

            post.VerificationStatus.Should().Be(PostVerificationStatus.CommunityVerified);
        }

        if (withdraw)
        {
            post.Withdraw(author, publishedAt.AddHours(1)).IsSuccess.Should().BeTrue();
        }

        dbContext.Set<CommunityPost>().Add(post);
        await dbContext.SaveChangesAsync();
    }

    private sealed record SafetyShape
    {
        public bool IsAvailable { get; init; }

        public int? Score { get; init; }

        public List<string> Limitations { get; init; } = [];

        public List<CountShape> Counts { get; init; } = [];
    }

    private sealed record CountShape
    {
        public string Kind { get; init; } = "";

        public string Source { get; init; } = "";

        public bool IsAvailable { get; init; }

        public string? UnavailableReason { get; init; }

        public DateTimeOffset? From { get; init; }

        public string Area { get; init; } = "";

        public string Method { get; init; } = "";

        public string? Attribution { get; init; }

        public int? Total { get; init; }

        public List<CategoryShape> Categories { get; init; } = [];
    }

    private sealed record CategoryShape
    {
        public string Id { get; init; } = "";

        public string Label { get; init; } = "";

        public int Count { get; init; }
    }
}
