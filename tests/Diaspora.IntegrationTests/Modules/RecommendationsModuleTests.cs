using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.IntegrationTests.Recommendations;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Policies;
using Diaspora.Modules.Recommendations.Domain.Strategies;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B14 driven over real HTTP through the same host every other test uses.
/// <para>
/// <b>The module plugged in without a single edit to the Platform Kernel, to any earlier module, or
/// to <c>Program.cs</c></b> — only a project reference so its assembly is in the build output. This
/// suite proves its endpoints, permissions, feature gates, EF configuration, catalogue seeding and
/// CQRS handlers all composed.
/// </para>
/// </summary>
public sealed class RecommendationsModuleTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RecommendationsModuleTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    // ── The catalogue ────────────────────────────────────────────────────

    /// <summary>
    /// <b>Without the seed the platform boots with no strategies and no policies, which is not a
    /// degraded state but a broken one.</b> Every surface would return an unranked pool with no
    /// exclusions: blocked content, muted categories and members' own listings would all appear.
    /// </summary>
    [Fact]
    public async Task The_catalogue_seeds_strategies_and_policies()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var strategies = await context.Set<RankingStrategy>().CountAsync();
        var policies = await context.Set<RecommendationPolicy>().CountAsync();

        strategies.Should().BeGreaterThan(0, "a surface with no strategies ranks nothing");
        policies.Should().BeGreaterThan(0, "a platform with no policies recommends everything");
    }

    /// <summary>
    /// <b>Seeding twice changes nothing.</b> The integration host calls the seeder again after
    /// creating the schema, and a deployment with a separate migration job does the same — so a
    /// seeder that was not idempotent would double every catalogue row on every restart.
    /// </summary>
    [Fact]
    public async Task Seeding_twice_does_not_duplicate_the_catalogue()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var before = await context.Set<RankingStrategy>().CountAsync();

        var seeder = Factory.Services
            .GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<Diaspora.Modules.Recommendations.Infrastructure.Hosting.RecommendationCatalogueSeeder>()
            .Single();

        await seeder.SeedAsync();

        using var second = Factory.Services.CreateScope();
        var after = await second.ServiceProvider
            .GetRequiredService<DiasporaDbContext>()
            .Set<RankingStrategy>()
            .CountAsync();

        after.Should().Be(before);
    }

    /// <summary>
    /// <b>Every seeded strategy has an implementation.</b> A configured key nothing implements
    /// contributes silent zeroes to every score on its surface — the worst kind of defect, because
    /// everything still works and the answers are quietly worse.
    /// </summary>
    [Fact]
    public async Task Every_seeded_strategy_is_implemented()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        using var scope = Factory.Services.CreateScope();
        var registry = scope.ServiceProvider
            .GetRequiredService<Diaspora.Modules.Recommendations.Extensibility.Ranking.IRankingStrategyRegistry>();

        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var configured = await context.Set<RankingStrategy>().Select(s => s.Key).Distinct().ToListAsync();

        configured.Should().OnlyContain(key => registry.IsImplemented(key));
    }

    // ── The member's surface ─────────────────────────────────────────────

    [Fact]
    public async Task A_member_can_read_their_feed()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/recommendations/feed");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var feed = await ReadDataAsync<HomeFeedShape>(response);

        feed.Should().NotBeNull();
        feed.IsPersonalized.Should().BeTrue("nothing has been opted out of");
    }

    /// <summary>
    /// <b>A member with no history gets a page rather than an error or an empty screen.</b> Cold
    /// start must not require history — that is the whole point of it, and it is the first thing a
    /// new member sees.
    /// </summary>
    [Fact]
    public async Task A_brand_new_member_gets_a_cold_start_page()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await ProjectCandidatesAsync(count: 6, countryCode: "MU");

        var response = await HttpClient.GetAsync("/api/v1/recommendations/guides");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await ReadDataAsync<RecommendationPageShape>(response);

        page.IsColdStart.Should().BeTrue();
        page.Items.Should().NotBeEmpty("a new member with no history is who cold start exists for");
    }

    /// <summary>
    /// <b>Every card carries an explanation.</b> The brief's central promise, checked over the wire
    /// rather than in the domain — because the promise is to the client.
    /// </summary>
    [Fact]
    public async Task Every_recommendation_arrives_with_a_reason_and_a_score()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await ProjectCandidatesAsync(count: 5, countryCode: "MU");

        var page = await ReadDataAsync<RecommendationPageShape>(
            await HttpClient.GetAsync("/api/v1/recommendations/guides"));

        page.Items.Should().NotBeEmpty();

        page.Items.Should().OnlyContain(
            item => item.Reason != null &&
                    !string.IsNullOrWhiteSpace(item.Reason.Code) &&
                    !string.IsNullOrWhiteSpace(item.Reason.ResourceKey) &&
                    !string.IsNullOrWhiteSpace(item.Reason.Text) &&
                    !string.IsNullOrWhiteSpace(item.Strategy),
            "the frontend must never receive an opaque recommendation");
    }

    /// <summary>
    /// <b>Dismissing something removes it from the next page.</b> The most noticeable behaviour in
    /// the module: a member who dismisses a card, refreshes and sees it again has watched the system
    /// visibly fail at the simplest thing it was asked.
    /// </summary>
    [Fact]
    public async Task A_dismissed_recommendation_does_not_come_back()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await ProjectCandidatesAsync(count: 5, countryCode: "MU");

        var first = await ReadDataAsync<RecommendationPageShape>(
            await HttpClient.GetAsync("/api/v1/recommendations/guides"));

        var dismissed = first.Items.Should().NotBeEmpty().And.Subject.First();

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/recommendations/dismiss",
            new
            {
                kind = "Dismissed",
                targetType = dismissed.TargetType,
                targetId = dismissed.TargetId,
                surface = "Guides",
                setId = first.SetId
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Forced, because the first page is still fresh and would otherwise be reused.
        var second = await ReadDataAsync<RecommendationPageShape>(
            await HttpClient.GetAsync("/api/v1/recommendations/guides?refresh=true"));

        second.Items.Should().NotContain(item => item.TargetId == dismissed.TargetId);
    }

    /// <summary>
    /// <b>A blocked target never appears again, and the block does not expire.</b> Distinct from a
    /// dismissal, which does — see <c>BlockedRecommendationTarget</c>.
    /// </summary>
    [Fact]
    public async Task A_blocked_target_never_appears()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await ProjectCandidatesAsync(count: 5, countryCode: "MU");

        var page = await ReadDataAsync<RecommendationPageShape>(
            await HttpClient.GetAsync("/api/v1/recommendations/guides"));

        var target = page.Items.First();

        var blocked = await HttpClient.PostAsJsonAsync(
            "/api/v1/recommendations/preferences/blocks",
            new { targetType = target.TargetType, targetId = target.TargetId },
            JsonOptions);

        blocked.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await ReadDataAsync<RecommendationPageShape>(
            await HttpClient.GetAsync("/api/v1/recommendations/guides?refresh=true"));

        after.Items.Should().NotContain(item => item.TargetId == target.TargetId);
    }

    // ── Signals and the profile ──────────────────────────────────────────

    /// <summary>
    /// <b>The whole loop, over HTTP: record signals, rebuild, see the profile move.</b> If this
    /// passes, the module's central mechanism works end to end through the real pipeline — validation,
    /// transaction, authorisation, audit and all.
    /// </summary>
    [Fact]
    public async Task Recorded_signals_reach_the_profile()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var recorded = await HttpClient.PostAsJsonAsync(
            "/api/v1/recommendations/signals",
            new
            {
                signals = new[]
                {
                    new { kind = "Viewed", key = "immigration", keyKind = "Category", countryCode = "MU" },
                    new { kind = "Saved", key = "immigration", keyKind = "Category", countryCode = "MU" }
                }
            },
            JsonOptions);

        recorded.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<SignalsRecordedShape>(recorded)).Accepted.Should().Be(2);

        var refreshed = await HttpClient.PostAsync("/api/v1/recommendations/profile/refresh", null);

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await ReadDataAsync<ProfileShape>(refreshed);

        profile.SignalCount.Should().BeGreaterThan(0);
        profile.Interests.Should().Contain(interest => interest.Key == "immigration");
    }

    /// <summary>
    /// <b>Turning signal sharing off actually stops the recording.</b> A privacy control that
    /// reports success and records anyway is worse than no control at all.
    /// </summary>
    [Fact]
    public async Task Turning_signal_sharing_off_drops_behavioural_signals()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var updated = await HttpClient.PutAsJsonAsync(
            "/api/v1/recommendations/preferences",
            new { shareActivitySignals = false },
            JsonOptions);

        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var recorded = await HttpClient.PostAsJsonAsync(
            "/api/v1/recommendations/signals",
            new { signals = new[] { new { kind = "Viewed", key = "housing", keyKind = "Category" } } },
            JsonOptions);

        var result = await ReadDataAsync<SignalsRecordedShape>(recorded);

        result.Accepted.Should().Be(0, "the member said no");
        result.Dropped.Should().Be(1);
        result.IsRecordingEnabled.Should().BeFalse("so the client can stop asking");
    }

    /// <summary>
    /// <b>The master switch reaches the feed.</b> Not "personalise less" — the profile stops being
    /// consulted and the response says so, which is what lets a client tell the member which feed
    /// they are looking at.
    /// </summary>
    [Fact]
    public async Task Turning_personalization_off_is_reported_on_the_feed()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await HttpClient.PutAsJsonAsync(
            "/api/v1/recommendations/preferences",
            new { isPersonalizationEnabled = false },
            JsonOptions);

        var feed = await ReadDataAsync<HomeFeedShape>(
            await HttpClient.GetAsync("/api/v1/recommendations/feed"));

        feed.IsPersonalized.Should().BeFalse();
    }

    /// <summary>
    /// <b>The rebuild is rate-limited, and the refusal says something useful.</b> A generic 429 tells
    /// a member they are doing something too much; this tells them their profile is already current.
    /// </summary>
    [Fact]
    public async Task A_second_rebuild_inside_the_cooldown_is_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        (await HttpClient.PostAsync("/api/v1/recommendations/profile/rebuild", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await HttpClient.PostAsync("/api/v1/recommendations/profile/rebuild", null);

        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    // ── Transparency ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_member_can_read_what_the_platform_believes_about_them()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/recommendations/profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<ProfileShape>(response)).Should().NotBeNull();
    }

    /// <summary>
    /// The reason catalogue is discoverable at runtime so a client can ship translations for codes
    /// it has not met yet — see <c>GetRecommendationReasonsQuery</c>.
    /// </summary>
    [Fact]
    public async Task The_reason_catalogue_is_discoverable()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var reasons = await ReadDataAsync<List<ReasonCatalogShape>>(
            await HttpClient.GetAsync("/api/v1/recommendations/reasons"));

        reasons.Should().NotBeEmpty();
        reasons.Should().OnlyContain(reason =>
            reason.ResourceKey.StartsWith("recommendations.reason.", StringComparison.Ordinal));
    }

    // ── Authorisation ────────────────────────────────────────────────────

    [Fact]
    public async Task An_anonymous_caller_cannot_read_a_feed()
    {
        var response = await HttpClient.GetAsync("/api/v1/recommendations/feed");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <b>An ordinary member cannot retune what everybody sees.</b> The line the module draws is
    /// "whose experience does this change", and it is enforced rather than documented.
    /// </summary>
    [Fact]
    public async Task An_ordinary_member_cannot_read_the_ranking_configuration()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/recommendations/operations/strategies");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    // ── Persistence ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>The generated set round-trips with its items and their score breakdowns.</b> Two levels of
    /// owned collection is exactly where EF's shadow-key convention is least visible and its failure
    /// least legible — an insert that becomes an update, reported as a concurrency conflict.
    /// </summary>
    [Fact]
    public async Task A_generated_set_round_trips_with_its_items_and_contributions()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await ProjectCandidatesAsync(count: 4, countryCode: "MU");

        var page = await ReadDataAsync<RecommendationPageShape>(
            await HttpClient.GetAsync("/api/v1/recommendations/guides"));

        page.SetId.Should().NotBeNull("a persisted set is what makes 'why am I seeing this' answerable");

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var stored = await context
            .Set<Diaspora.Modules.Recommendations.Domain.Sets.RecommendationSet>()
            .Include(set => set.Items)
            .ThenInclude(item => item.Contributions)
            .FirstOrDefaultAsync(set => set.Id == RecommendationSetId.From(page.SetId!.Value));

        stored.Should().NotBeNull();
        stored!.Items.Should().NotBeEmpty();
        stored.Items.Should().OnlyContain(item => item.Explanation != null);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Projects candidates directly.
    /// <para>
    /// Through the repository rather than by publishing an integration event, because the consumers
    /// are exercised in their own suite and driving eleven verticals' publication flows to test a
    /// ranking would make every test here depend on every module in the platform.
    /// </para>
    /// </summary>
    private async Task ProjectCandidatesAsync(int count, string countryCode)
    {
        using var scope = Factory.Services.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IRecommendationCandidateRepository>();

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<Domain.Abstractions.IUnitOfWork>();

        for (var index = 0; index < count; index++)
        {
            var candidate = await repository.GetOrProjectAsync(
                RecommendationTargetType.Guide,
                Guid.NewGuid(),
                "Guides",
                $"Guide {index}",
                CancellationToken.None);

            candidate.Describe(categoryKey: "immigration");
            candidate.Place(countryCode);
            candidate.Rate(0.5m + (index * 0.05m), 0.5m, 100 + index);
            candidate.Restrict(CandidateVisibility.Public);
            candidate.Schedule(clock.UtcNow.AddDays(-index - 1), clock.UtcNow.AddDays(-index - 1));

            candidate.SetAttributes(
                [CandidateAttribute.Create(Guid.NewGuid(), InterestKind.Tag, "visa")]);

            repository.Update(candidate);
        }

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);
}
