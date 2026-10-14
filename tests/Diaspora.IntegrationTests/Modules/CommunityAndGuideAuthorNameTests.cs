using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Members see who wrote things (14 Oct 2026). Community and Guides store an author identifier and
/// no name, and neither asked Identity for one, so every post, comment, feed card and guide was
/// shown as "Community member" — the reader's own included. The name now comes from
/// <c>IMemberProfileService</c>, resolved once per response.
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class CommunityAndGuideAuthorNameTests : PooledIdentityTestBase
{
    /// <summary>The full name <c>RegistrationFor</c> gives every test member.</summary>
    private const string MemberName = "Amara Okafor";

    public CommunityAndGuideAuthorNameTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_post_its_comments_and_its_feed_card_carry_the_authors_name()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        using var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body = "Whose post is this?", category = "Traffic", urgency = "Normal", publish = true }));
        var post = created.RootElement.GetProperty("data");
        var postId = post.GetProperty("postId").GetGuid();

        AuthorName(post).Should().Be(MemberName, "the create response names its author");

        using var fetched = await DataAsync(
            await HttpClient.GetAsync($"/api/v1/community/posts/{postId}"));
        AuthorName(fetched.RootElement.GetProperty("data")).Should().Be(MemberName);

        using var added = await DataAsync(await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/posts/{postId}/comments", new { body = "Mine, as it happens." }));
        var comment = added.RootElement.GetProperty("data");
        comment.GetProperty("authorName").GetString().Should().Be(MemberName);
        AuthorName(comment).Should().Be(MemberName);

        using var thread = await DataAsync(
            await HttpClient.GetAsync($"/api/v1/community/posts/{postId}/comments"));
        var root = thread.RootElement.GetProperty("data").GetProperty("comments")[0];
        root.GetProperty("authorName").GetString().Should().Be(MemberName);
        AuthorName(root).Should().Be(MemberName);

        using var feed = await DataAsync(
            await HttpClient.GetAsync("/api/v1/community/feeds/home"));
        var item = feed.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(candidate => candidate.GetProperty("contentId").GetGuid() == postId);
        AuthorName(item.GetProperty("post")).Should().Be(MemberName);
    }

    [Fact]
    public async Task A_guide_card_and_its_byline_carry_the_authors_name()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        using var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/guides",
            new { title = "Renewing a passport abroad", categoryKey = "immigration", language = "en", countryCode = "NG" }));
        var guideId = created.RootElement.GetProperty("data").GetProperty("guideId").GetGuid();

        using var drafts = await DataAsync(await HttpClient.GetAsync("/api/v1/guides/mine/drafts"));
        var card = drafts.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(candidate => candidate.GetProperty("guideId").GetGuid() == guideId);
        card.GetProperty("authorName").GetString().Should().Be(MemberName);

        using var detail = await DataAsync(await HttpClient.GetAsync($"/api/v1/guides/{guideId}"));
        detail.RootElement.GetProperty("data").GetProperty("author").GetProperty("name").GetString()
            .Should().Be(MemberName, "a member Trust has not projected yet still has a byline");
    }

    private static string? AuthorName(JsonElement element) =>
        element.GetProperty("author").GetProperty("name").GetString();

    private static async Task<JsonDocument> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);

        return JsonDocument.Parse(body);
    }
}
