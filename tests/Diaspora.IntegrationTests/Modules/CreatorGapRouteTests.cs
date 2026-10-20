using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The three routes the app simulated in test builds until 20 Oct 2026, now served for real:
/// creator recommendations with a persisted dismissal, a creator's sponsored content, and the
/// platform overview's totals as a time series.
/// <para>
/// <b>Every positive assertion is against data produced through real routes</b> — a guide published
/// through B8, a milestone completed by the outbox delivering that publication, a campaign drafted
/// through B15 — for the reason the Creator Platform suite records: a route that answers an empty
/// table correctly can still throw on its first row.
/// </para>
/// <para>
/// Wire shapes are re-declared by hand, so these tests fail when the JSON changes rather than
/// whenever the module's own records do.
/// </para>
/// </summary>
public sealed class CreatorGapRouteTests : IdentityTestBase
{
    private const string Recommendations = "/api/v1/recommendations";
    private const string Creators = "/api/v1/creators";
    private const string Analytics = "/api/v1/analytics";
    private const string GuidesRoot = "/api/v1/guides";
    private const string Financial = "/api/v1/financial";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CreatorGapRouteTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // GET /recommendations/creator and the dismissal
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A member with no creator record gets an answer, not a 404, and no record is opened by asking.
    /// </summary>
    [Fact]
    public async Task A_new_member_reads_recommendations_without_a_creator_record()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync($"{Recommendations}/creator");
        var items = await ReadDataAsync<List<RecommendationDto>>(response);

        items.Should().NotContain(
            item => item.RecommendationId.StartsWith("milestone:", StringComparison.Ordinal),
            "a member who has done nothing has no milestone to claim");

        items.Should().OnlyHaveUniqueItems(item => item.RecommendationId);
    }

    /// <summary>
    /// <b>The milestone the outbox completed is the first suggestion.</b> Publishing a guide
    /// completes <c>first-guide</c>; unclaimed, it is served with a stable key, the claim screen's
    /// route, and full confidence.
    /// </summary>
    [Fact]
    public async Task A_completed_unclaimed_milestone_is_recommended_with_a_stable_key()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Milestone guide");
        await Factory.DrainOutboxAsync();

        var milestone = await FirstGuideMilestoneAsync();
        milestone.State.Should().Be("Completed", "the publication was delivered and counted");

        var items = await ReadDataAsync<List<RecommendationDto>>(
            await HttpClient.GetAsync($"{Recommendations}/creator"));

        var claim = items.Should().ContainSingle(
            item => item.RecommendationId == $"milestone:{milestone.MilestoneId:D}").Subject;

        claim.Kind.Should().Be("improvement");
        claim.ActionRoute.Should().Be("/creator/milestones");
        claim.ActionLabel.Should().NotBeNullOrWhiteSpace();
        claim.Body.Should().Contain(milestone.Title);
        claim.Reason.Should().NotBeNullOrWhiteSpace();
        claim.Confidence.Should().Be(1.0);

        items[0].RecommendationId.Should().Be(
            claim.RecommendationId, "a claimable reward is the most certain suggestion there is");

        // The creator record exists now, so the profile gaps are served too, one key per field.
        items.Should().Contain(item => item.RecommendationId == "profile:bio");
    }

    /// <summary>
    /// <b>Dismissal is a round trip and it sticks.</b> Dismissed through the client's path, it is
    /// gone on the next read; dismissed again, it is still one dismissal; and the canonical creator
    /// path dismisses another.
    /// </summary>
    [Fact]
    public async Task A_dismissed_recommendation_stays_dismissed()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Dismissal guide");
        await Factory.DrainOutboxAsync();

        var milestone = await FirstGuideMilestoneAsync();
        var key = $"milestone:{milestone.MilestoneId:D}";

        var dismissed = await ReadDataAsync<DismissedDto>(
            await HttpClient.PostAsync(
                $"{Recommendations}/{Uri.EscapeDataString(key)}/dismiss", content: null));

        dismissed.RecommendationId.Should().Be(key);

        var after = await ReadDataAsync<List<RecommendationDto>>(
            await HttpClient.GetAsync($"{Recommendations}/creator"));

        after.Should().NotContain(item => item.RecommendationId == key);

        var again = await HttpClient.PostAsync(
            $"{Recommendations}/{Uri.EscapeDataString(key)}/dismiss", content: null);

        again.StatusCode.Should().Be(HttpStatusCode.OK, "a second dismissal is the same dismissal");

        var bio = await HttpClient.PostAsync(
            $"{Recommendations}/creator/{Uri.EscapeDataString("profile:bio")}/dismiss", content: null);

        bio.StatusCode.Should().Be(HttpStatusCode.OK);

        var last = await ReadDataAsync<List<RecommendationDto>>(
            await HttpClient.GetAsync($"{Recommendations}/creator?take=50"));

        last.Should().NotContain(item => item.RecommendationId == key);
        last.Should().NotContain(item => item.RecommendationId == "profile:bio");
    }

    /// <summary>A dismissal is the dismisser's own: another member still sees theirs.</summary>
    [Fact]
    public async Task One_members_dismissal_does_not_hide_anothers_recommendation()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Mine");
        await Factory.DrainOutboxAsync();

        await HttpClient.PostAsync(
            $"{Recommendations}/creator/{Uri.EscapeDataString("profile:bio")}/dismiss", content: null);

        await AuthenticateAsync();
        await PublishGuideAsync("Theirs");
        await Factory.DrainOutboxAsync();

        var items = await ReadDataAsync<List<RecommendationDto>>(
            await HttpClient.GetAsync($"{Recommendations}/creator?take=50"));

        items.Should().Contain(item => item.RecommendationId == "profile:bio");
    }

    /// <summary>Once claimed, a milestone is not a suggestion any more.</summary>
    [Fact]
    public async Task A_claimed_milestone_is_no_longer_recommended()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Claimed guide");
        await Factory.DrainOutboxAsync();

        var milestone = await FirstGuideMilestoneAsync();

        var claimed = await HttpClient.PostAsJsonAsync(
            $"{Creators}/me/milestones/{milestone.MilestoneId}/claim", new { }, JsonOptions);

        claimed.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await ReadDataAsync<List<RecommendationDto>>(
            await HttpClient.GetAsync($"{Recommendations}/creator?take=50"));

        items.Should().NotContain(item => item.RecommendationId == $"milestone:{milestone.MilestoneId:D}");
    }

    [Fact]
    public async Task Take_bounds_the_list()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Take guide");
        await Factory.DrainOutboxAsync();

        var items = await ReadDataAsync<List<RecommendationDto>>(
            await HttpClient.GetAsync($"{Recommendations}/creator?take=2"));

        items.Should().HaveCount(2, "a milestone and several profile gaps are available");
    }

    [Fact]
    public async Task Recommendations_refuse_an_anonymous_caller()
    {
        var response = await Factory.CreateClient().GetAsync($"{Recommendations}/creator");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>B11's own one-segment dismissal is untouched by the two-segment routes.</summary>
    [Fact]
    public async Task The_feed_dismissal_is_still_the_recommendation_modules_own()
    {
        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Recommendations}/dismiss",
            new { targetType = "Guide", targetId = Guid.NewGuid(), surface = "home" },
            JsonOptions);

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        response.StatusCode.Should().NotBe(HttpStatusCode.MethodNotAllowed);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // GET /creators/me/sponsored
    // ════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_new_creator_has_no_sponsored_content()
    {
        await AuthenticateAsync();

        var items = await ReadDataAsync<List<SponsoredDto>>(
            await HttpClient.GetAsync($"{Creators}/me/sponsored"));

        items.Should().BeEmpty();
    }

    /// <summary>
    /// <b>A campaign over the creator's own guide is on the list, titled by B8.</b> Drafted through
    /// B15's real route; nothing paid, so no amount; a campaign is not renewable; the guide's title
    /// comes from the Guides module rather than from the campaign.
    /// </summary>
    [Fact]
    public async Task A_campaign_promoting_a_guide_is_listed_with_the_guides_own_title()
    {
        await AuthenticateAsync();
        var guideId = await PublishGuideAsync("Registering with a GP");

        var created = await HttpClient.PostAsJsonAsync(
            $"{Financial}/campaigns",
            new
            {
                name = "GP guide push",
                objective = "Traffic",
                budgetMinorUnits = 5_000,
                currency = "GBP",
                exponent = 2,
                productCode = "advertising.campaign.standard.30d",
                startsAt = DateTimeOffset.UtcNow.AddDays(1),
                endsAt = DateTimeOffset.UtcNow.AddDays(31),
                audience = new { countryCodes = new[] { "GB" } },
                headline = "New in London? Read this first",
                subjectType = "Guide",
                subjectId = guideId
            },
            JsonOptions);

        var campaign = await ReadDataAsync<CampaignIdDto>(created);

        var items = await ReadDataAsync<List<SponsoredDto>>(
            await HttpClient.GetAsync($"{Creators}/me/sponsored"));

        var item = items.Should().ContainSingle().Subject;

        item.CampaignId.Should().Be(campaign.Id);
        item.PlacementId.Should().BeNull("a campaign creates no B15 placement");
        item.ContentId.Should().Be(guideId);
        item.ContentType.Should().Be("Guide");
        item.ContentTitle.Should().Be("Registering with a GP", "the title is the guide's, from B8");
        item.ProductCode.Should().Be("advertising.campaign.standard.30d");
        item.PackageLabel.Should().NotBeNullOrWhiteSpace("the catalogue names the product");
        item.State.Should().Be("Draft");
        item.AmountPaid.Should().BeNull("nothing has been paid");
        item.IsRenewable.Should().BeFalse();
        item.Performance.Should().BeNull("B15 has recorded no delivery");

        var pending = await ReadDataAsync<List<SponsoredDto>>(
            await HttpClient.GetAsync($"{Creators}/me/sponsored?state=pending"));

        pending.Should().ContainSingle();

        var live = await ReadDataAsync<List<SponsoredDto>>(
            await HttpClient.GetAsync($"{Creators}/me/sponsored?state=live"));

        live.Should().BeEmpty();
    }

    /// <summary>A campaign promoting nothing in particular is advertising, not sponsored content.</summary>
    [Fact]
    public async Task A_campaign_without_a_subject_is_not_sponsored_content()
    {
        await AuthenticateAsync();

        await ReadDataAsync<CampaignIdDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Financial}/campaigns",
                new
                {
                    name = "Brand",
                    objective = "Awareness",
                    budgetMinorUnits = 5_000,
                    currency = "GBP",
                    exponent = 2,
                    productCode = "advertising.campaign.standard.30d",
                    startsAt = DateTimeOffset.UtcNow.AddDays(1),
                    endsAt = DateTimeOffset.UtcNow.AddDays(31),
                    audience = new { countryCodes = new[] { "GB" } },
                    headline = "Hello"
                },
                JsonOptions));

        var items = await ReadDataAsync<List<SponsoredDto>>(
            await HttpClient.GetAsync($"{Creators}/me/sponsored"));

        items.Should().BeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // GET /analytics/platform
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A published guide moves the total, the period's additions and today's point.</b> Asserted
    /// as differences, because the suite's other tests publish guides into the same database.
    /// </summary>
    [Fact]
    public async Task Platform_totals_reflect_a_published_guide()
    {
        await AuthenticateAsync();

        var before = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/platform?period=7d"));

        await PublishGuideAsync("Counted on the platform");

        var after = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/platform?period=7d"));

        after.Subject.Type.Should().Be("platform");
        after.Status.Should().Be("LIVE", "every source is registered on this host");
        after.UnavailableMetrics.Should().BeEmpty();

        Metric(after, "inventory.guides").Should().Be(Metric(before, "inventory.guides") + 1);
        Metric(after, "growth.guides.new").Should().Be(Metric(before, "growth.guides.new") + 1);
        Metric(after, "growth.content.new").Should().Be(Metric(before, "growth.content.new") + 1);

        var series = after.Series.Single(entry => entry.Key == "growth.guides.new");
        series.Granularity.Should().Be("day");
        series.Points.Should().HaveCount(7, "seven daily buckets for seven days");

        var beforeSeries = before.Series.Single(entry => entry.Key == "growth.guides.new");
        series.Points[^1].Value.Should().Be(beforeSeries.Points[^1].Value + 1, "today's bucket");
        series.Points[^1].IsPartialBucket.Should().BeTrue("today has not finished");

        after.Metrics.Select(metric => metric.Key).Should().Contain(
        [
            "inventory.jobs", "inventory.listings", "inventory.housing", "inventory.providers",
            "audience.users.total", "audience.users.new",
            "growth.jobs.new", "growth.listings.new", "growth.housing.new", "growth.providers.new"
        ]);

        after.Metrics.Should().OnlyContain(metric => metric.Value.Source == "backend");
    }

    /// <summary>A member who registers is counted, and the total is never below the period's joiners.</summary>
    [Fact]
    public async Task Platform_member_counts_include_a_new_member()
    {
        await AuthenticateAsync();

        var before = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/platform?period=30d"));

        await RegisterVerifiedMemberAsync();
        await AuthenticateAsync();

        var after = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/platform?period=30d"));

        Metric(after, "audience.users.new").Should().BeGreaterThanOrEqualTo(
            Metric(before, "audience.users.new") + 2);

        Metric(after, "audience.users.total").Should().BeGreaterThanOrEqualTo(
            Metric(after, "audience.users.new"));

        after.Range.Preset.Should().Be("last30d");
    }

    [Fact]
    public async Task Ninety_days_is_served_in_weekly_buckets()
    {
        await AuthenticateAsync();

        var dataset = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/platform?period=90d"));

        dataset.Series.Should().OnlyContain(series => series.Granularity == "week");
        dataset.Series.Should().OnlyContain(series => series.Points.Count == 13);
    }

    [Fact]
    public async Task An_unknown_period_is_refused()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync($"{Analytics}/platform?period=forever");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Platform_analytics_refuse_an_anonymous_caller()
    {
        var response = await Factory.CreateClient().GetAsync($"{Analytics}/platform");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task AuthenticateAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private async Task<MilestoneDto> FirstGuideMilestoneAsync()
    {
        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        return milestones.Single(milestone => milestone.Key == "first-guide");
    }

    private async Task<Guid> PublishGuideAsync(string title)
    {
        var draft = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                GuidesRoot,
                new { title, categoryKey = "getting-started", language = "en", countryCode = "GB" },
                JsonOptions));

        await HttpClient.PutAsJsonAsync(
            $"{GuidesRoot}/{draft.GuideId}",
            new
            {
                title,
                countryCode = "GB",
                sections = new[]
                {
                    new
                    {
                        sectionId = Guid.NewGuid(),
                        title = "What to do",
                        order = 0,
                        blocks = new[]
                        {
                            new
                            {
                                blockId = Guid.NewGuid(),
                                kind = "Markdown",
                                order = 0,
                                content = "Take the form, the fee and one hour of patience."
                            }
                        }
                    }
                }
            },
            JsonOptions);

        var published = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                $"{GuidesRoot}/{draft.GuideId}/publish",
                new { revisionNotes = "First edition." },
                JsonOptions));

        published.Status.Should().Be("Published");

        return published.GuideId;
    }

    private static double Metric(DatasetDto dataset, string key) =>
        dataset.Metrics.Single(metric => metric.Key == key).Value.Value;

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded, but answered {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    // ── Wire shapes, re-declared by hand ────────────────────────────────────────────────────

    private sealed record GuideDto(Guid GuideId, string Status);

    private sealed record MilestoneDto(Guid MilestoneId, string Key, string Title, string State);

    private sealed record RecommendationDto(
        string RecommendationId,
        string Kind,
        string Title,
        string Body,
        string Reason,
        string? Icon,
        string ActionLabel,
        string ActionRoute,
        double? Confidence,
        DateTimeOffset? ExpiresAt);

    private sealed record DismissedDto(string RecommendationId, DateTimeOffset DismissedAt);

    private sealed record CampaignIdDto(Guid Id);

    private sealed record MoneyDto(decimal Amount, string Currency, long MinorUnits, int Exponent);

    private sealed record MetricDto(string Key, string Label, double Value, string Format);

    private sealed record SponsoredDto(
        Guid? PlacementId,
        Guid? CampaignId,
        Guid ContentId,
        string ContentType,
        string ContentTitle,
        string? ProductCode,
        string? PackageLabel,
        string State,
        string? RejectionReason,
        MoneyDto? AmountPaid,
        DateTimeOffset? StartsAt,
        DateTimeOffset? EndsAt,
        bool IsRenewable,
        List<MetricDto>? Performance);

    private sealed record SubjectDto(string Type);

    private sealed record RangeDto(string Preset, string From, string To, string Timezone);

    private sealed record SourcedDto(double Value, string Source);

    private sealed record DatasetMetricDto(string Key, string Label, SourcedDto Value, string Format);

    private sealed record PointDto(string At, double Value, bool IsPartialBucket);

    private sealed record SeriesDto(string Key, string Granularity, List<PointDto> Points);

    private sealed record DatasetDto(
        SubjectDto Subject,
        RangeDto Range,
        string Status,
        List<DatasetMetricDto> Metrics,
        List<SeriesDto> Series,
        List<string> UnavailableMetrics);
}
