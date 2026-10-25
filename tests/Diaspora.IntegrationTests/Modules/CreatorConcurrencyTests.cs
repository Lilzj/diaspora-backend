using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B18.x's concurrency guarantees, against a host that can actually serve two requests at once.
/// <para>
/// <b>These are in their own fixture because the suite's ordinary host cannot run them.</b> Every
/// other module test shares one <c>SqliteConnection</c>, and two requests arriving together make the
/// driver throw from inside the authentication middleware — a 500 before the request reaches any
/// module. See <see cref="ConcurrentWebApplicationFactory"/>.
/// </para>
/// <para>
/// <b>What is under test is the database constraint, not the handler's lookup.</b> Every one of
/// these handlers looks its idempotency key up before writing, and two concurrent requests both find
/// nothing and both proceed — which is the point. What stops a second claim, a second enrolment and
/// a second debit is the unique index; what turns the resulting violation into an answer rather than
/// a 500 is the idempotency guard. A test that only proved the lookup works would prove the easy
/// half.
/// </para>
/// </summary>
public sealed class CreatorConcurrencyTests : IdentityTestBase<ConcurrentWebApplicationFactory>
{
    private const string Creators = "/api/v1/creators";
    private const string Challenges = "/api/v1/challenges";
    private const string Rewards = "/api/v1/rewards";
    private const string Financial = "/api/v1/financial";
    private const string GuidesRoot = "/api/v1/guides";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The member <see cref="AuthenticateAsync"/> last signed in, whose points a test vests.</summary>
    private Guid _memberUserId;

    public CreatorConcurrencyTests(ConcurrentWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// <b>Two concurrent claims of one milestone pay once.</b> One request wins; the other either
    /// replays it or reports the first as still in flight, and neither answer is a second reward.
    /// </summary>
    [Fact]
    public async Task Concurrent_claims_of_one_milestone_pay_once()
    {
        var token = await AuthenticateAsync();

        await PublishGuideAsync(token, "Concurrent claim");
        await Factory.DrainOutboxAsync();

        using var first = Client(token);
        using var second = Client(token);

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await first.GetAsync($"{Creators}/me/milestones"));

        var milestone = milestones.Single(row => row.Key == "first-guide");

        milestone.State.Should().Be("Completed");

        var path = $"{Creators}/me/milestones/{milestone.MilestoneId}/claim";
        var payload = new { idempotencyKey = Guid.NewGuid().ToString("N") };

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync(path, payload, JsonOptions),
            second.PostAsJsonAsync(path, payload, JsonOptions));

        try
        {
            responses.Count(response => response.IsSuccessStatusCode)
                .Should().BeGreaterThan(0, "one of the two must have landed");

            foreach (var response in responses.Where(response => !response.IsSuccessStatusCode))
            {
                response.StatusCode.Should().Be(
                    HttpStatusCode.Conflict,
                    "the loser is a conflict, never a 500 and never a second claim");
            }

            await Factory.DrainOutboxAsync();

            var balance = await ReadDataAsync<RewardBalanceDto>(
                await first.GetAsync($"{Financial}/rewards/me"));

            balance.PendingPoints.Should().Be(
                100, "the milestone pays a hundred points, once, however many callers asked");

            var after = await ReadDataAsync<List<MilestoneDto>>(
                await first.GetAsync($"{Creators}/me/milestones"));

            after.Single(row => row.Key == "first-guide").State.Should().Be("Claimed");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>
    /// <b>Two concurrent joins produce one enrolment.</b> The unique index on (challenge, creator) is
    /// what decides it, and it decides it even when neither request carried a key.
    /// </summary>
    [Fact]
    public async Task Concurrent_joins_of_one_challenge_produce_one_enrolment()
    {
        var token = await AuthenticateAsync();

        using var first = Client(token);
        using var second = Client(token);

        var challenges = await ReadDataAsync<List<ChallengeDto>>(
            await first.GetAsync(Challenges));

        var challenge = challenges.Single(row => row.Key == "settling-in-sprint");
        var path = $"{Challenges}/{challenge.ChallengeId}/join";

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync(path, new { }, JsonOptions),
            second.PostAsJsonAsync(path, new { }, JsonOptions));

        try
        {
            responses.Count(response => response.IsSuccessStatusCode)
                .Should().BeGreaterThan(0);

            foreach (var response in responses.Where(response => !response.IsSuccessStatusCode))
            {
                response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            }

            // One enrolment, whichever request wrote it: the member reads a single joined challenge.
            var active = await ReadDataAsync<List<ChallengeDto>>(
                await first.GetAsync($"{Challenges}?state=active"));

            active.Count(row => row.Key == "settling-in-sprint")
                .Should().Be(1, "two joins are one enrolment");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>
    /// <b>Two concurrent redemptions of one reward spend the points once.</b> The most important of
    /// these three: the balance is B15's, the debit happens inside the redemption's own transaction,
    /// and the unique index on the idempotency key is what stops the second one.
    /// </summary>
    [Fact]
    public async Task Concurrent_redemptions_spend_the_points_once()
    {
        var token = await AuthenticateAsync();

        await Factory.DrainOutboxAsync();

        using var first = Client(token);
        using var second = Client(token);

        await first.PostAsJsonAsync(
            $"{Rewards}/signup-bonus/acknowledge", new { }, JsonOptions);

        for (var index = 0; index < 10; index++)
        {
            await PublishGuideAsync(token, $"Concurrent redemption {index}");
        }

        await Factory.DrainOutboxAsync();

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await first.GetAsync($"{Creators}/me/milestones"));

        foreach (var milestone in milestones.Where(row => row.State == "Completed"))
        {
            await first.PostAsJsonAsync(
                $"{Creators}/me/milestones/{milestone.MilestoneId}/claim", new { }, JsonOptions);
        }

        await Factory.DrainOutboxAsync();
        await Factory.VestRewardPointsAsync(_memberUserId);

        var before = await ReadDataAsync<RewardBalanceDto>(
            await first.GetAsync($"{Financial}/rewards/me"));

        before.Balance.Should().BeGreaterThanOrEqualTo(300);

        var catalogue = await ReadDataAsync<RewardCatalogueDto>(
            await first.GetAsync($"{Rewards}/catalogue"));

        var reward = catalogue.Rewards.Single(row => row.Key == "creator-badge-supporter");

        var payload = new
        {
            rewardId = reward.RewardId,
            idempotencyKey = Guid.NewGuid().ToString("N")
        };

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync($"{Rewards}/redemptions", payload, JsonOptions),
            second.PostAsJsonAsync($"{Rewards}/redemptions", payload, JsonOptions));

        try
        {
            responses.Count(response => response.IsSuccessStatusCode).Should().BeGreaterThan(0);

            foreach (var response in responses.Where(response => !response.IsSuccessStatusCode))
            {
                response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            }

            var after = await ReadDataAsync<RewardBalanceDto>(
                await first.GetAsync($"{Financial}/rewards/me"));

            after.Balance.Should().Be(
                before.Balance - reward.PointsCost,
                "the points come out once, however many callers asked");

            var history = await ReadDataAsync<List<RedemptionDto>>(
                await first.GetAsync($"{Rewards}/redemptions/me"));

            history.Should().ContainSingle("two requests produced one redemption");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<string> AuthenticateAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        _memberUserId = auth.User.Id;

        return auth.Tokens.AccessToken;
    }

    private HttpClient Client(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        return client;
    }

    private async Task PublishGuideAsync(string token, string title)
    {
        using var client = Client(token);

        var draft = await ReadDataAsync<GuideDto>(
            await client.PostAsJsonAsync(
                GuidesRoot,
                new { title, categoryKey = "getting-started", language = "en", countryCode = "GB" },
                JsonOptions));

        await client.PutAsJsonAsync(
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

        await client.PostAsJsonAsync(
            $"{GuidesRoot}/{draft.GuideId}/publish",
            new { revisionNotes = "First edition." },
            JsonOptions);
    }

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

    private sealed record ProgressDto(long Current, long Target, int Percentage);

    private sealed record MilestoneDto(
        Guid MilestoneId, string Key, string State, ProgressDto Progress);

    private sealed record ChallengeDto(Guid ChallengeId, string Key, string State);

    private sealed record RewardDto(Guid RewardId, string Key, int PointsCost, string State);

    private sealed record RewardCatalogueDto(List<RewardDto> Rewards, long AvailablePoints);

    private sealed record RedemptionDto(Guid RedemptionId, int PointsSpent, string State);

    private sealed record RewardBalanceDto(long Balance, long LifetimeEarned, long PendingPoints = 0);
}
