using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Publishing a post as an organisation or community, over real HTTP (20 Oct 2026).
/// <para>
/// <c>publishAsOrganizationId</c> was stored unchecked, so anybody could post as any community, and
/// the community's moderation queue reads such a post as its own. The id is a Community Programme
/// community or a Community Life organisation; each module now says who may speak for it, and
/// everybody else is refused with <c>community.post.publisher-forbidden</c> rather than quietly
/// posted as themselves.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityPublishAsTests : PooledIdentityTestBase
{
    private const string Communities = "/api/v1/communities";
    private const string Posts = "/api/v1/community/posts";
    private const string Organizations = "/api/v1/community-life/organizations";
    private const string Forbidden = "community.post.publisher-forbidden";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityPublishAsTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// A stranger, a plain member and a moderator are all refused. Moderating a community is not
    /// speaking for it.
    /// </summary>
    [Fact]
    public async Task Only_an_administrator_may_publish_as_a_community()
    {
        var community = await SeedCommunityAsync("Publish refused");

        var (stranger, _) = await MemberClientAsync();
        var (member, memberId) = await MemberClientAsync();
        var (moderator, moderatorId) = await MemberClientAsync();

        await JoinAsync(member, memberId, community, role: null);
        await JoinAsync(moderator, moderatorId, community, CommunityMembershipRole.Moderator);

        foreach (var client in new[] { stranger, member, moderator })
        {
            var response = await PostAsync(client, "Speaking for the community.", community.Id);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await response.Content.ReadAsStringAsync()).Should().Contain(Forbidden);
        }

        (await ReadDataAsync<List<JsonElement>>(
            await moderator.GetAsync($"{Communities}/{community.Slug}/reports")))
            .Should().BeEmpty("no post was published as the community, so nothing can be reported");
    }

    /// <summary>
    /// An id that is neither a community nor an organisation is refused the same way — never
    /// stored, and never turned into a personal post.
    /// </summary>
    [Fact]
    public async Task An_unknown_publisher_is_refused()
    {
        var (member, _) = await MemberClientAsync();

        var response = await PostAsync(member, "Speaking for nobody.", Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain(Forbidden);
    }

    /// <summary>
    /// <b>An administrator publishes as the community</b>, the byline says so, and when the post is
    /// reported the report lands in that community's moderation queue.
    /// </summary>
    [Fact]
    public async Task An_administrator_publishes_as_the_community_and_reports_reach_its_queue()
    {
        var community = await SeedCommunityAsync("Publish granted");

        var (administrator, administratorId) = await MemberClientAsync();
        await JoinAsync(administrator, administratorId, community, CommunityMembershipRole.Administrator);

        var (moderator, moderatorId) = await MemberClientAsync();
        await JoinAsync(moderator, moderatorId, community, CommunityMembershipRole.Moderator);

        var (reporter, _) = await MemberClientAsync();

        var post = await ReadDataAsync<PostDto>(
            await PostAsync(administrator, "A notice from the community.", community.Id));

        post.Author.UserId.Should().Be(administratorId, "the member who pressed publish stays accountable");
        post.Author.OrganizationId.Should().Be(community.Id);
        post.Author.OrganizationName.Should().Be(community.Name);
        post.Author.PublishingIdentityType.Should().Be("community");

        var reportId = await ReportAsync(reporter, post.PostId);

        var queue = await ReadDataAsync<List<ReportDto>>(
            await moderator.GetAsync($"{Communities}/{community.Slug}/reports"));

        queue.Should().ContainSingle();
        queue[0].Id.Should().Be(reportId);
        queue[0].ContentId.Should().Be(post.PostId);
    }

    /// <summary>
    /// <b>Personal posts are unaffected</b>: a member with no role anywhere still posts as
    /// themselves, and their post reaches no community's queue.
    /// </summary>
    [Fact]
    public async Task A_personal_post_needs_no_publisher_and_reaches_no_community_queue()
    {
        var community = await SeedCommunityAsync("Publish personal");

        var (moderator, moderatorId) = await MemberClientAsync();
        await JoinAsync(moderator, moderatorId, community, CommunityMembershipRole.Moderator);

        var (author, authorId) = await MemberClientAsync();
        var (reporter, _) = await MemberClientAsync();

        var post = await ReadDataAsync<PostDto>(await PostAsync(author, "Just me.", publishAs: null));

        post.Author.UserId.Should().Be(authorId);
        post.Author.OrganizationId.Should().BeNull();
        post.Author.PublishingIdentityType.Should().Be("personal");

        await ReportAsync(reporter, post.PostId);

        (await ReadDataAsync<List<ReportDto>>(
            await moderator.GetAsync($"{Communities}/{community.Slug}/reports")))
            .Should().BeEmpty();
    }

    /// <summary>
    /// <b>The organisation id space.</b> The owner of a published Community Life organisation may
    /// publish as it; a member with no standing there may not; and nobody may publish as an
    /// organisation that is still a draft.
    /// </summary>
    [Fact]
    public async Task An_organisation_is_spoken_for_by_its_organizers_once_it_is_published()
    {
        var (owner, _) = await MemberClientAsync();
        var (stranger, _) = await MemberClientAsync();

        var organizationName = $"Association {Guid.NewGuid():N}";
        var organizationId = await CreateOrganizationAsync(owner, organizationName);

        var whileDraft = await PostAsync(owner, "Too early.", organizationId);

        whileDraft.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "a draft organisation does not speak for anyone yet");

        (await owner.PostAsJsonAsync($"{Organizations}/{organizationId}/publish", new { }))
            .EnsureSuccessStatusCode();

        var post = await ReadDataAsync<PostDto>(
            await PostAsync(owner, "From the association.", organizationId));

        post.Author.OrganizationId.Should().Be(organizationId);
        post.Author.OrganizationName.Should().Be(organizationName);
        post.Author.PublishingIdentityType.Should().Be("organization");

        var refused = await PostAsync(stranger, "Not my association.", organizationId);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain(Forbidden);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<(HttpClient Client, Guid UserId)> MemberClientAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        return (client, auth.User.Id);
    }

    /// <summary>Joins over HTTP, then is granted a role the way an owner would grant it.</summary>
    private async Task JoinAsync(
        HttpClient client, Guid userId, SeededCommunity community, CommunityMembershipRole? role)
    {
        (await client.PostAsJsonAsync($"{Communities}/{community.Slug}/membership", new { action = "join" }))
            .EnsureSuccessStatusCode();

        if (role is null)
        {
            return;
        }

        using var scope = Factory.Services.CreateScope();
        var memberships = scope.ServiceProvider.GetRequiredService<ICommunityMembershipRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var membership = await memberships.FindAsync(
            CommunityId.From(community.Id), UserId.From(userId));

        membership!.SetVisibility(CommunityMembershipVisibility.CommunityOnly);
        membership.Grant(role.Value).IsSuccess.Should().BeTrue();

        await unitOfWork.SaveChangesAsync();
    }

    private async Task<SeededCommunity> SeedCommunityAsync(string name)
    {
        var slug = CommunitySlug.FromName($"{name}-{Guid.NewGuid():N}").Value;

        using var scope = Factory.Services.CreateScope();
        var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var community = CommunityAggregate.Propose(
            CommunityId.New(), slug, name,
            CommunityType.NationalDiaspora, CommunityVisibility.Public, "NG").Value;

        community.Open();

        communities.Add(community);
        await unitOfWork.SaveChangesAsync();

        return new SeededCommunity(community.Id.Value, slug.Value, name);
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient owner, string name)
    {
        var response = await owner.PostAsJsonAsync(
            Organizations,
            new
            {
                name,
                summary = "A body created by the integration suite.",
                organizationTypeKey = "cultural-association",
                countryCode = "GB",
                cityName = "London"
            });

        return (await ReadDataAsync<OrganizationDto>(response)).OrganizationId;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string body, Guid? publishAs) =>
        client.PostAsJsonAsync(
            Posts,
            new
            {
                body,
                category = "Traffic",
                urgency = "Normal",
                publish = true,
                publishAsOrganizationId = publishAs
            });

    private static async Task<Guid> ReportAsync(HttpClient client, Guid postId)
    {
        var report = await ReadDataAsync<FiledReportDto>(
            await client.PostAsJsonAsync(
                "/api/v1/community/reports",
                new { contentType = "post", contentId = postId, reason = "spam" }));

        return report.ReportId;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "the request should have succeeded, and the body says why it did not: {0}", body);

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record SeededCommunity(Guid Id, string Slug, string Name);

    private sealed record OrganizationDto(Guid OrganizationId);

    private sealed record AuthorDto(
        Guid UserId, Guid? OrganizationId, string? OrganizationName, string? PublishingIdentityType);

    private sealed record PostDto(Guid PostId, AuthorDto Author);

    private sealed record FiledReportDto(Guid ReportId);

    private sealed record ReportDto(Guid Id, Guid ContentId);
}
