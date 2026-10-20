using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Following a contributor (20 Oct 2026).
/// <para>
/// Before this, a follow was stored and nothing else happened: Creator's tally compared Community's
/// <c>member</c> against <c>User</c> and never counted it, an unfollow was never published, a button
/// could not ask whether it was already following, a byline had no way to reach a creator record, and
/// nobody was told when someone they followed published a guide.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class FollowContributorTests : PooledIdentityTestBase
{
    /// <summary>The full name <c>RegistrationFor</c> gives every test member.</summary>
    private const string MemberName = "Amara Okafor";

    public FollowContributorTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_byline_reaches_the_creator_record_by_user_id()
    {
        var (_, author) = await RegisterVerifiedMemberAsync();
        await Factory.DrainOutboxAsync();

        var (_, reader) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reader.Tokens.AccessToken);

        using var creator = await DataAsync(
            await HttpClient.GetAsync($"/api/v1/creators/by-user/{author.User.Id}"));
        var data = creator.RootElement.GetProperty("data");

        data.GetProperty("userId").GetGuid().Should().Be(author.User.Id);
        data.GetProperty("creatorId").GetGuid().Should().NotBe(
            author.User.Id, "the creator identifier is the record's own, which is why the lookup exists");

        var missing = await HttpClient.GetAsync($"/api/v1/creators/by-user/{Guid.NewGuid()}");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await missing.Content.ReadAsStringAsync()).Should().Contain("creator.profile.not-found");
    }

    [Fact]
    public async Task Following_is_readable_counted_by_creator_and_undone_by_unfollowing()
    {
        var (_, author) = await RegisterVerifiedMemberAsync();
        var (_, follower) = await RegisterVerifiedMemberAsync();
        await Factory.DrainOutboxAsync();

        var target = author.User.Id;
        Api.Authenticate(follower.Tokens.AccessToken);

        var before = await StateAsync(target);
        before.IsFollowing.Should().BeFalse();
        before.FollowerCount.Should().Be(0);

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/community/follows/{target}", new { targetType = "member" }))
            .IsSuccessStatusCode.Should().BeTrue();

        var after = await StateAsync(target);
        after.IsFollowing.Should().BeTrue();
        after.FollowerCount.Should().Be(1);

        await Factory.DrainOutboxAsync();
        (await CreatorFollowersAsync(target)).Should().Be(
            1, "Community publishes \"member\" and Creator now counts it");

        (await HttpClient.DeleteAsync($"/api/v1/community/follows/{target}?targetType=member"))
            .IsSuccessStatusCode.Should().BeTrue();

        var undone = await StateAsync(target);
        undone.IsFollowing.Should().BeFalse();
        undone.FollowerCount.Should().Be(0);

        await Factory.DrainOutboxAsync();
        (await CreatorFollowersAsync(target)).Should().Be(
            0, "the unfollow is published now and the tally comes back down");
    }

    [Fact]
    public async Task The_follow_state_needs_a_signed_in_caller()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync(
            $"/api/v1/community/follows/{Guid.NewGuid()}?targetType=member");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_guide_published_by_a_followed_author_is_announced_to_the_follower_only()
    {
        var (_, author) = await RegisterVerifiedMemberAsync();
        var (_, follower) = await RegisterVerifiedMemberAsync();
        var (_, muted) = await RegisterVerifiedMemberAsync();
        var (_, stranger) = await RegisterVerifiedMemberAsync();
        await Factory.DrainOutboxAsync();

        Api.Authenticate(follower.Tokens.AccessToken);
        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/community/follows/{author.User.Id}", new { targetType = "member" }))
            .IsSuccessStatusCode.Should().BeTrue();

        Api.Authenticate(muted.Tokens.AccessToken);
        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/community/follows/{author.User.Id}",
                new { targetType = "member", muted = true }))
            .IsSuccessStatusCode.Should().BeTrue();

        var title = $"Opening a bank account {Guid.NewGuid():N}";

        Api.Authenticate(author.Tokens.AccessToken);
        var guideId = await PublishGuideAsync(title);

        await Factory.DrainOutboxAsync();

        var inbox = await InboxAsync(follower.Tokens.AccessToken);
        inbox.Should().Contain($"{MemberName} published a new guide: {title}", "the follower is told");
        inbox.Should().Contain($"/guide/{guideId}");

        (await InboxAsync(muted.Tokens.AccessToken)).Should().NotContain(
            title, "a muted follow asked not to hear from this author");
        (await InboxAsync(stranger.Tokens.AccessToken)).Should().NotContain(title);
        (await InboxAsync(author.Tokens.AccessToken)).Should().NotContain(
            $"published a new guide: {title}", "the author is not told about their own guide");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<(bool IsFollowing, int FollowerCount)> StateAsync(Guid target)
    {
        using var state = await DataAsync(await HttpClient.GetAsync(
            $"/api/v1/community/follows/{target}?targetType=member"));
        var data = state.RootElement.GetProperty("data");

        return (data.GetProperty("isFollowing").GetBoolean(),
            data.GetProperty("followerCount").GetInt32());
    }

    private async Task<long> CreatorFollowersAsync(Guid userId)
    {
        using var creator = await DataAsync(
            await HttpClient.GetAsync($"/api/v1/creators/by-user/{userId}"));

        return creator.RootElement.GetProperty("data").GetProperty("followers").GetInt64();
    }

    private async Task<Guid> PublishGuideAsync(string title)
    {
        using var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/guides",
            new { title, categoryKey = "immigration", language = "en", countryCode = "NG" }));
        var guideId = created.RootElement.GetProperty("data").GetProperty("guideId").GetGuid();

        using var updated = await DataAsync(await HttpClient.PutAsJsonAsync(
            $"/api/v1/guides/{guideId}",
            new
            {
                title,
                countryCode = "NG",
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
                                content = "Bring a passport and proof of address."
                            }
                        }
                    }
                }
            }));

        using var published = await DataAsync(await HttpClient.PostAsJsonAsync(
            $"/api/v1/guides/{guideId}/publish", new { revisionNotes = "First edition." }));

        return guideId;
    }

    private async Task<string> InboxAsync(string accessToken)
    {
        Api.Authenticate(accessToken);

        // The raw page: the envelope's shape is not what these assertions are about.
        return await HttpClient.GetStringAsync("/api/v1/notifications?limit=50");
    }

    private static async Task<JsonDocument> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);

        return JsonDocument.Parse(body);
    }
}
