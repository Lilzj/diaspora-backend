using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// <b>A grant that cannot be announced is not a claim.</b>
/// <para>
/// Financial pays a member only from <c>RewardPointsGrantedIntegrationEvent</c>. The Creator relay
/// used to swallow a failed publish, so a milestone claim committed as Claimed with nothing behind it:
/// the member was told they were paid, never were, and every retry replayed "Claimed". The grant now
/// fails the claim, the claim rolls back, and a later attempt pays exactly once.
/// </para>
/// </summary>
public sealed class CreatorGrantPublishTests : IdentityTestBase<GrantRefusingWebApplicationFactory>
{
    private const string Creators = "/api/v1/creators";
    private const string Financial = "/api/v1/financial";
    private const string GuidesRoot = "/api/v1/guides";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CreatorGrantPublishTests(GrantRefusingWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_points_grant_that_cannot_be_enqueued_rolls_the_claim_back()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await PublishGuideAsync("Unannounced grant");
        await Factory.DrainOutboxAsync();

        var first = await FirstGuideAsync();
        first.State.Should().Be("Completed");

        var path = $"{Creators}/me/milestones/{first.MilestoneId}/claim";

        Factory.RefuseGrants = true;
        try
        {
            var refused = await HttpClient.PostAsJsonAsync(path, new { }, JsonOptions);

            refused.IsSuccessStatusCode.Should().BeFalse("the payment behind the claim could not be announced");
        }
        finally
        {
            Factory.RefuseGrants = false;
        }

        (await FirstGuideAsync()).State.Should().Be(
            "Completed", "a claim whose payment was never announced must not read as claimed");

        var retried = await ReadDataAsync<MilestoneDto>(
            await HttpClient.PostAsJsonAsync(path, new { }, JsonOptions));

        retried.State.Should().Be("Claimed");

        await Factory.DrainOutboxAsync();

        var balance = await ReadDataAsync<RewardBalanceDto>(
            await HttpClient.GetAsync($"{Financial}/rewards/me"));

        balance.LifetimeEarned.Should().Be(
            100, "the first-guide milestone, paid once: the refused attempt left nothing behind");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<MilestoneDto> FirstGuideAsync() =>
        (await ReadDataAsync<List<MilestoneDto>>(await HttpClient.GetAsync($"{Creators}/me/milestones")))
            .Single(milestone => milestone.Key == "first-guide");

    private async Task PublishGuideAsync(string title)
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

        await HttpClient.PostAsJsonAsync(
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

    private sealed record MilestoneDto(Guid MilestoneId, string Key, string State, ProgressDto Progress);

    private sealed record RewardBalanceDto(long Balance, long LifetimeEarned);
}
