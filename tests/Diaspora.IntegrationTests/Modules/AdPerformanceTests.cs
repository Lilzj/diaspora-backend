using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Recommendations.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Application.Advertising;
using Diaspora.Modules.Financial.Application.Consumers.Advertising;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Campaign performance — MON-GAP-002, end to end.
/// <para>
/// <b>The register's reason this gap could not be closed was that nothing recorded an impression
/// anywhere.</b> The most important test here is therefore not a read: it is that B14 saying "I
/// served this because it was promoted" turns into a number an advertiser can see. A performance
/// route over a counter nobody increments would report zero for ever, which is worse than the
/// honest simulated marker it replaced — and this platform already has a recorder in that state
/// (<c>GuideAnalyticsRecorder.RecordSearchImpressionsAsync</c>, which nothing calls).
/// </para>
/// </summary>
public sealed class AdPerformanceTests : IdentityTestBase
{
    private const string Root = "/api/v1/financial";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AdPerformanceTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── The loop ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The whole point of the increment.</b> B14 publishes what it served as promoted; B15 counts
    /// it; the advertiser's dashboard shows it.
    /// </summary>
    [Fact]
    public async Task A_promotion_B14_served_becomes_an_impression_the_advertiser_can_see()
    {
        var subjectId = Guid.NewGuid();
        var (client, campaign) = await DeliveringCampaignAsync(subjectId);
        using var _ = client;

        await ConsumeAsync(new RecommendationGeneratedIntegrationEvent
        {
            SetId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Surface = "Home",
            ItemCount = 10,
            StrategyKeys = ["rule-weighting"],
            IsColdStart = false,
            PolicyExclusionCount = 0,
            PromotedTargets =
            [
                new PromotedTargetPayload
                {
                    TargetType = "MarketplaceListing",
                    TargetId = subjectId,
                    Position = 1
                }
            ],
            GeneratedAt = DateTimeOffset.UtcNow
        });

        var performance = await ReadDataAsync<PerformanceDto>(
            await client.GetAsync($"{Root}/campaigns/{campaign}/performance"));

        performance.Impressions.Should().Be(1);
        performance.BySurface.Should().ContainSingle()
            .Which.Surface.Should().Be("Home");
    }

    [Fact]
    public async Task A_set_with_nothing_promoted_records_nothing()
    {
        var (client, campaign) = await DeliveringCampaignAsync(Guid.NewGuid());
        using var _ = client;

        await ConsumeAsync(new RecommendationGeneratedIntegrationEvent
        {
            SetId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Surface = "Home",
            ItemCount = 10,
            StrategyKeys = ["rule-weighting"],
            IsColdStart = false,
            PolicyExclusionCount = 0,
            PromotedTargets = [],
            GeneratedAt = DateTimeOffset.UtcNow
        });

        var performance = await ReadDataAsync<PerformanceDto>(
            await client.GetAsync($"{Root}/campaigns/{campaign}/performance"));

        performance.Impressions.Should().Be(0);
    }

    /// <summary>
    /// <b>A promotion with no campaign above it is skipped, not counted against somebody else.</b>
    /// Every boost bought before campaigns existed is one of these — a real promotion with nothing
    /// to attribute it to.
    /// </summary>
    [Fact]
    public async Task A_promoted_subject_with_no_campaign_is_skipped()
    {
        var (client, campaign) = await DeliveringCampaignAsync(Guid.NewGuid());
        using var _ = client;

        await ConsumeAsync(new RecommendationGeneratedIntegrationEvent
        {
            SetId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Surface = "Home",
            ItemCount = 1,
            StrategyKeys = ["rule-weighting"],
            IsColdStart = false,
            PolicyExclusionCount = 0,
            PromotedTargets =
            [
                new PromotedTargetPayload
                {
                    TargetType = "MarketplaceListing",

                    // Nobody's campaign.
                    TargetId = Guid.NewGuid(),
                    Position = 1
                }
            ],
            GeneratedAt = DateTimeOffset.UtcNow
        });

        var performance = await ReadDataAsync<PerformanceDto>(
            await client.GetAsync($"{Root}/campaigns/{campaign}/performance"));

        performance.Impressions.Should().Be(0);
    }

    [Fact]
    public async Task Impressions_accumulate_across_sets_and_bucket_by_surface()
    {
        var subjectId = Guid.NewGuid();
        var (client, campaign) = await DeliveringCampaignAsync(subjectId);
        using var _ = client;

        await ConsumeAsync(Served(subjectId, "Home"));
        await ConsumeAsync(Served(subjectId, "Home"));
        await ConsumeAsync(Served(subjectId, "Search"));

        var performance = await ReadDataAsync<PerformanceDto>(
            await client.GetAsync($"{Root}/campaigns/{campaign}/performance"));

        performance.Impressions.Should().Be(3);

        performance.BySurface.Should().HaveCount(2);
        performance.BySurface.Single(row => row.Surface == "Home").Impressions.Should().Be(2);
        performance.BySurface.Single(row => row.Surface == "Search").Impressions.Should().Be(1);

        performance.Daily.Should().ContainSingle("all three landed on the same UTC day");
    }

    // ── Clicks ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_click_is_recorded_and_a_rate_appears()
    {
        var subjectId = Guid.NewGuid();
        var (client, campaign) = await DeliveringCampaignAsync(subjectId);
        using var _ = client;

        await ConsumeAsync(Served(subjectId, "Home"));
        await ConsumeAsync(Served(subjectId, "Home"));

        var recorded = await client.PostAsJsonAsync(
            $"{Root}/campaigns/{campaign}/clicks", new { surface = "Home" });

        recorded.StatusCode.Should().Be(HttpStatusCode.OK);

        var performance = await ReadDataAsync<PerformanceDto>(
            await client.GetAsync($"{Root}/campaigns/{campaign}/performance"));

        performance.Clicks.Should().Be(1);
        performance.ClickThroughRate.Should().Be(0.5m);
    }

    /// <summary>
    /// <b>A click against a campaign that is not running is refused.</b> Otherwise an advertiser
    /// would be shown numbers for delivery they had already stopped paying for.
    /// </summary>
    [Fact]
    public async Task A_click_against_a_campaign_that_is_not_running_is_refused()
    {
        using var client = await AdvertiserAsync();
        var draft = await CreateDraftAsync(client, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            $"{Root}/campaigns/{draft}/clicks", new { surface = "Home" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("ad_campaign.not_delivering");
    }

    // ── What the numbers mean ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The definitions travel with the figures.</b> MON-GAP-002 asks for them by name, and a
    /// definition that lives in a code comment is one the person reading the dashboard never sees.
    /// </summary>
    [Fact]
    public async Task Every_response_carries_the_metric_definitions()
    {
        var (client, campaign) = await DeliveringCampaignAsync(Guid.NewGuid());
        using var _ = client;

        var performance = await ReadDataAsync<PerformanceDto>(
            await client.GetAsync($"{Root}/campaigns/{campaign}/performance"));

        performance.Definitions.Impression.Should().Contain("because it was promoted");
        performance.Definitions.Deduplication.Should().Contain("None");
        performance.Definitions.ReportingLag.Should().NotBeNullOrWhiteSpace();

        performance.Definitions.Impression.Should().Contain(
            "not what was looked at",
            "served is not seen, and the platform says which one it counts");
    }

    /// <summary>
    /// <b>An absent rate is null rather than zero.</b> "0% clicked" and "we have not shown this to
    /// anybody yet" are different sentences, and only one is a reason to change the creative.
    /// </summary>
    [Fact]
    public async Task A_campaign_that_has_served_nothing_reports_no_rate_rather_than_zero()
    {
        var (client, campaign) = await DeliveringCampaignAsync(Guid.NewGuid());
        using var _ = client;

        var performance = await ReadDataAsync<PerformanceDto>(
            await client.GetAsync($"{Root}/campaigns/{campaign}/performance"));

        performance.Impressions.Should().Be(0);
        performance.ClickThroughRate.Should().BeNull();
        performance.CostPerResult.Should().BeNull("dividing by no results is not free");
    }

    // ── Ownership ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Performance_is_the_most_sensitive_read_and_a_stranger_gets_nothing()
    {
        var (owner, campaign) = await DeliveringCampaignAsync(Guid.NewGuid());
        using var _ = owner;

        using var stranger = await AdvertiserAsync();

        var response = await stranger.GetAsync($"{Root}/campaigns/{campaign}/performance");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_summary_rolls_up_only_the_callers_own_campaigns()
    {
        var mineSubject = Guid.NewGuid();
        var (mine, _) = await DeliveringCampaignAsync(mineSubject);
        using var owner = mine;

        var theirsSubject = Guid.NewGuid();
        var (theirs, _) = await DeliveringCampaignAsync(theirsSubject);
        theirs.Dispose();

        await ConsumeAsync(Served(mineSubject, "Home"));
        await ConsumeAsync(Served(theirsSubject, "Home"));
        await ConsumeAsync(Served(theirsSubject, "Home"));

        var summary = await ReadDataAsync<PerformanceDto>(
            await owner.GetAsync($"{Root}/placements/me/performance/summary"));

        summary.Impressions.Should().Be(1, "the other advertiser's two are not the caller's");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static RecommendationGeneratedIntegrationEvent Served(Guid subjectId, string surface) =>
        new()
        {
            SetId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Surface = surface,
            ItemCount = 5,
            StrategyKeys = ["rule-weighting"],
            IsColdStart = false,
            PolicyExclusionCount = 0,
            PromotedTargets =
            [
                new PromotedTargetPayload
                {
                    TargetType = "MarketplaceListing",
                    TargetId = subjectId,
                    Position = 1
                }
            ],
            GeneratedAt = DateTimeOffset.UtcNow
        };

    /// <summary>
    /// Drives the consumer directly rather than through the outbox, so the test asserts the
    /// mapping and the counting rather than the dispatcher's timing.
    /// </summary>
    private async Task ConsumeAsync(RecommendationGeneratedIntegrationEvent integrationEvent)
    {
        using var scope = Factory.Services.CreateScope();

        var consumer = ActivatorUtilities.CreateInstance<PromotedImpressionConsumer>(
            scope.ServiceProvider);

        await consumer.Handle(
            new IntegrationEventNotification<RecommendationGeneratedIntegrationEvent>(
                integrationEvent),
            CancellationToken.None);
    }

    private async Task<HttpClient> AdvertiserAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        return client;
    }

    private async Task<Guid> CreateDraftAsync(HttpClient client, Guid subjectId)
    {
        var created = await ReadDataAsync<CampaignDto>(
            await client.PostAsJsonAsync($"{Root}/campaigns", new
            {
                name = "Performance probe",
                objective = "Traffic",
                budgetMinorUnits = 50_000,
                currency = "GBP",
                exponent = 2,
                startsAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                endsAt = DateTimeOffset.UtcNow.AddDays(30),
                subjectType = "MarketplaceListing",
                subjectId
            }));

        return created.Id;
    }

    /// <summary>
    /// A campaign in <c>Active</c>, which is the only state impressions and clicks are counted in.
    /// <para>
    /// Driven through the aggregate rather than the routes because approval is a staff act and the
    /// point of this suite is the counters, not the review queue — which has its own tests.
    /// </para>
    /// </summary>
    private async Task<(HttpClient Client, Guid CampaignId)> DeliveringCampaignAsync(Guid subjectId)
    {
        var client = await AdvertiserAsync();
        var campaignId = await CreateDraftAsync(client, subjectId);

        using var scope = Factory.Services.CreateScope();
        var campaigns = scope.ServiceProvider.GetRequiredService<IAdCampaignRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var campaign = await campaigns.FindAsync(AdCampaignId.From(campaignId));

        var now = DateTimeOffset.UtcNow;
        campaign!.Submit(now);
        campaign.MarkFunded(MonetizationPurchaseId.New());
        campaign.Approve(Guid.NewGuid(), null, now);
        campaign.MarkScheduled();
        campaign.Activate(now);

        await unitOfWork.SaveChangesAsync();

        return (client, campaignId);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "the request should have succeeded, and the body says why it did not: {0}", body);

        var envelope = JsonSerializer.Deserialize<EnvelopeDto<T>>(body, JsonOptions);

        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();

        return envelope.Data!;
    }

    // ── Wire shapes, re-declared. ───────────────────────────────────────────────────────────

    private sealed record EnvelopeDto<T>(T? Data);

    private sealed record CampaignDto(Guid Id, string Status);

    private sealed record MoneyDto(long MinorUnits, string Currency, int Exponent);

    private sealed record DefinitionsDto(
        string Impression,
        string Click,
        string Conversion,
        string Result,
        string Deduplication,
        string ReportingLag);

    private sealed record DailyDto(
        DateOnly Day, long Impressions, long Clicks, long Conversions, long SpendMinorUnits);

    private sealed record SurfaceDto(string Surface, long Impressions, long Clicks);

    private sealed record PerformanceDto(
        DateOnly From,
        DateOnly To,
        long Impressions,
        long Clicks,
        long Conversions,
        decimal? ClickThroughRate,
        MoneyDto Spend,
        long Results,
        MoneyDto? CostPerResult,
        decimal? BudgetConsumedFraction,
        List<DailyDto> Daily,
        List<SurfaceDto> BySurface,
        DefinitionsDto Definitions);
}
