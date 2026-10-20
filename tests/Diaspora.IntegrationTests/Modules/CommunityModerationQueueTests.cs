using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
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
/// FRONTEND-PHASE13-GAP-017 — a community's own moderation queue, over real HTTP (21 Oct 2026).
/// <para>
/// A community's content is what was published as the community: a post whose publisher is the
/// community. Every test here files a real report through <c>POST /community/reports</c> and reads it
/// back through <c>/communities/{slug}/reports</c>, so the join between the two modules is what is
/// under test, not either side alone.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityModerationQueueTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/communities";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityModerationQueueTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// <b>The moderator sees their community's report and nobody else's</b> — not another
    /// community's, not a member's personal post — and the row carries no reporter identity.
    /// </summary>
    [Fact]
    public async Task A_moderator_sees_only_their_own_communitys_reports()
    {
        var mine = await SeedAsync("Queue mine");
        var theirs = await SeedAsync("Queue theirs");

        var (moderator, _) = await ModeratorOfAsync(mine);
        var (author, _) = await PublisherOfAsync(mine, theirs);
        var (reporter, _) = await MemberClientAsync();

        var minePost = await PostAsync(author, "Published as my community.", mine.Id);
        var theirPost = await PostAsync(author, "Published as theirs.", theirs.Id);
        var personalPost = await PostAsync(author, "Published as myself.", null);

        var reportId = await ReportAsync(reporter, minePost);
        await ReportAsync(reporter, theirPost);
        await ReportAsync(reporter, personalPost);

        var response = await moderator.GetAsync($"{Root}/{mine.Slug}/reports");
        var raw = await response.Content.ReadAsStringAsync();
        var rows = await ReadDataAsync<List<ReportDto>>(response);

        rows.Should().ContainSingle("one report was filed about this community's content");

        var row = rows[0];
        row.Id.Should().Be(reportId);
        row.CommunityId.Should().Be(mine.Id);
        row.ContentId.Should().Be(minePost);
        row.ContentType.Should().Be("post");
        row.Status.Should().Be("Open");
        row.Reason.Should().Be("Spam");
        row.ReporterCount.Should().Be(1);
        row.ContentExcerpt.Should().Contain("Published as my community");

        raw.Should().NotContain("reporterId", "a community moderator is never told who reported");
        raw.Should().NotContain("description");
    }

    /// <summary>
    /// <b>A moderator's removal goes through the platform's decision</b>: the post is removed, the
    /// report is answered, and the queue's Open view empties while its Actioned view fills.
    /// </summary>
    [Fact]
    public async Task A_moderator_can_remove_reported_content_and_the_queue_follows()
    {
        var community = await SeedAsync("Queue remove");
        var (moderator, _) = await ModeratorOfAsync(community);
        var (author, _) = await PublisherOfAsync(community);
        var (reporter, _) = await MemberClientAsync();
        var (second, _) = await MemberClientAsync();

        var post = await PostAsync(author, "Buy followers here.", community.Id);
        var reportId = await ReportAsync(reporter, post);
        await ReportAsync(second, post);

        var before = await ReadDataAsync<List<ReportDto>>(
            await moderator.GetAsync($"{Root}/{community.Slug}/reports?status=Open"));

        before.Should().ContainSingle("two reports about one post are one decision");
        before[0].ReporterCount.Should().Be(2);
        before[0].Id.Should().Be(reportId, "the row is named by its oldest report");

        var decided = await ReadDataAsync<ReportDto>(
            await moderator.PostAsJsonAsync(
                $"{Root}/{community.Slug}/reports/{reportId}/decision",
                new { decision = "remove-content", note = "Spam." }));

        decided.Status.Should().Be("Actioned");
        decided.ReporterCount.Should().Be(2, "both reports were answered by the one decision");
        decided.Note.Should().Be("Spam.");

        (await ReadDataAsync<List<ReportDto>>(
            await moderator.GetAsync($"{Root}/{community.Slug}/reports?status=Open")))
            .Should().BeEmpty();

        (await ReadDataAsync<List<ReportDto>>(
            await moderator.GetAsync($"{Root}/{community.Slug}/reports?status=Actioned")))
            .Should().ContainSingle();

        (await PostStatusAsync(post)).Should().Be(PostStatus.Removed);

        // Decided once. A second decision on the same report is refused, not applied twice.
        var again = await moderator.PostAsJsonAsync(
            $"{Root}/{community.Slug}/reports/{reportId}/decision", new { decision = "dismiss" });

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>Dismissing answers the report and leaves a published post published.</summary>
    [Fact]
    public async Task Dismissing_leaves_the_content_up()
    {
        var community = await SeedAsync("Queue dismiss");
        var (moderator, _) = await ModeratorOfAsync(community);
        var (author, _) = await PublisherOfAsync(community);
        var (reporter, _) = await MemberClientAsync();

        var post = await PostAsync(author, "A perfectly fine notice.", community.Id);
        var reportId = await ReportAsync(reporter, post);

        var decided = await ReadDataAsync<ReportDto>(
            await moderator.PostAsJsonAsync(
                $"{Root}/{community.Slug}/reports/{reportId}/decision", new { decision = "dismiss" }));

        decided.Status.Should().Be("Dismissed");
        (await PostStatusAsync(post)).Should().Be(PostStatus.Published);
    }

    /// <summary>
    /// Escalating hands the report to the platform's moderators: it reads Escalated here, and the
    /// community can no longer decide it.
    /// </summary>
    [Fact]
    public async Task Escalating_hands_the_report_to_the_platform()
    {
        var community = await SeedAsync("Queue escalate");
        var (moderator, _) = await ModeratorOfAsync(community);
        var (author, _) = await PublisherOfAsync(community);
        var (reporter, _) = await MemberClientAsync();

        var post = await PostAsync(author, "Needs a staff decision.", community.Id);
        var reportId = await ReportAsync(reporter, post);

        var escalated = await ReadDataAsync<ReportDto>(
            await moderator.PostAsJsonAsync(
                $"{Root}/{community.Slug}/reports/{reportId}/decision", new { decision = "escalate" }));

        escalated.Status.Should().Be("Escalated");

        var then = await moderator.PostAsJsonAsync(
            $"{Root}/{community.Slug}/reports/{reportId}/decision", new { decision = "remove-content" });

        then.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await then.Content.ReadAsStringAsync()).Should().Contain("community.report.escalated");
        (await PostStatusAsync(post)).Should().Be(PostStatus.Published);
    }

    /// <summary>
    /// Warn is refused rather than recorded: nothing can deliver a warning, and a report marked
    /// warned when nobody was told is a decision that only looks taken.
    /// </summary>
    [Fact]
    public async Task Warn_is_refused_and_the_report_stays_open()
    {
        var community = await SeedAsync("Queue warn");
        var (moderator, _) = await ModeratorOfAsync(community);
        var (author, _) = await PublisherOfAsync(community);
        var (reporter, _) = await MemberClientAsync();

        var post = await PostAsync(author, "Borderline.", community.Id);
        var reportId = await ReportAsync(reporter, post);

        var response = await moderator.PostAsJsonAsync(
            $"{Root}/{community.Slug}/reports/{reportId}/decision", new { decision = "warn" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("warn-unavailable");

        (await ReadDataAsync<List<ReportDto>>(
            await moderator.GetAsync($"{Root}/{community.Slug}/reports?status=Open")))
            .Should().ContainSingle();
    }

    /// <summary>A member who does not moderate the community is refused both routes.</summary>
    [Fact]
    public async Task A_member_who_does_not_moderate_is_forbidden()
    {
        var community = await SeedAsync("Queue forbidden");
        var (author, _) = await PublisherOfAsync(community);
        var (reporter, _) = await MemberClientAsync();
        var (member, _) = await MemberClientAsync();

        await member.PostAsJsonAsync($"{Root}/{community.Slug}/membership", new { action = "join" });

        var post = await PostAsync(author, "Reported.", community.Id);
        var reportId = await ReportAsync(reporter, post);

        var list = await member.GetAsync($"{Root}/{community.Slug}/reports");

        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await list.Content.ReadAsStringAsync()).Should().Contain("moderation.forbidden");

        var decide = await member.PostAsJsonAsync(
            $"{Root}/{community.Slug}/reports/{reportId}/decision", new { decision = "remove-content" });

        decide.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await PostStatusAsync(post)).Should().Be(PostStatus.Published);
    }

    /// <summary>
    /// <b>A moderator of one community cannot decide another's report</b> by naming its id under
    /// their own community — the answer is not-found, and the report is untouched.
    /// </summary>
    [Fact]
    public async Task A_report_from_another_community_is_refused()
    {
        var mine = await SeedAsync("Queue cross mine");
        var theirs = await SeedAsync("Queue cross theirs");

        var (moderator, _) = await ModeratorOfAsync(mine);
        var (theirModerator, _) = await ModeratorOfAsync(theirs);
        var (author, _) = await PublisherOfAsync(theirs);
        var (reporter, _) = await MemberClientAsync();

        var theirPost = await PostAsync(author, "Belongs to the other community.", theirs.Id);
        var theirReport = await ReportAsync(reporter, theirPost);

        var response = await moderator.PostAsJsonAsync(
            $"{Root}/{mine.Slug}/reports/{theirReport}/decision", new { decision = "remove-content" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("community.report.not-found");

        (await PostStatusAsync(theirPost)).Should().Be(PostStatus.Published);

        var stillOpen = await ReadDataAsync<List<ReportDto>>(
            await theirModerator.GetAsync($"{Root}/{theirs.Slug}/reports?status=Open"));

        stillOpen.Should().ContainSingle(row => row.Id == theirReport);

        // And the other community's moderator cannot reach it through its own community's queue
        // either: their routes are not a back door into this one.
        var wrongWay = await theirModerator.GetAsync($"{Root}/{mine.Slug}/reports");
        wrongWay.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A status the client does not declare is refused rather than read as "all".</summary>
    [Fact]
    public async Task An_unknown_status_is_refused()
    {
        var community = await SeedAsync("Queue status");
        var (moderator, _) = await ModeratorOfAsync(community);

        var response = await moderator.GetAsync($"{Root}/{community.Slug}/reports?status=Pending");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    /// <summary>A member who joined over HTTP and was then made a moderator, the way an owner would.</summary>
    private async Task<(HttpClient Client, Guid UserId)> ModeratorOfAsync(SeededCommunity community)
    {
        var (client, userId) = await MemberClientAsync();

        await GrantAsync(client, userId, community, CommunityMembershipRole.Moderator);

        return (client, userId);
    }

    /// <summary>
    /// A member made an Administrator of each community — somebody who may publish as it. Since
    /// 20 Oct 2026 nobody else may, so a post "as the community" needs one of these as its author.
    /// </summary>
    private async Task<(HttpClient Client, Guid UserId)> PublisherOfAsync(
        params SeededCommunity[] communities)
    {
        var (client, userId) = await MemberClientAsync();

        foreach (var community in communities)
        {
            await GrantAsync(client, userId, community, CommunityMembershipRole.Administrator);
        }

        return (client, userId);
    }

    /// <summary>Joins over HTTP, then is granted a role the way an owner would grant it.</summary>
    private async Task GrantAsync(
        HttpClient client, Guid userId, SeededCommunity community, CommunityMembershipRole? role)
    {
        (await client.PostAsJsonAsync($"{Root}/{community.Slug}/membership", new { action = "join" }))
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

    private async Task<SeededCommunity> SeedAsync(string name)
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

        return new SeededCommunity(community.Id.Value, slug.Value);
    }

    private static async Task<Guid> PostAsync(HttpClient client, string body, Guid? publishAs)
    {
        var post = await ReadDataAsync<PostDto>(
            await client.PostAsJsonAsync(
                "/api/v1/community/posts",
                new
                {
                    body,
                    category = "Traffic",
                    urgency = "Normal",
                    publish = true,
                    publishAsOrganizationId = publishAs
                }));

        return post.PostId;
    }

    private static async Task<Guid> ReportAsync(HttpClient client, Guid postId)
    {
        var report = await ReadDataAsync<FiledReportDto>(
            await client.PostAsJsonAsync(
                "/api/v1/community/reports",
                new { contentType = "post", contentId = postId, reason = "spam" }));

        return report.ReportId;
    }

    private async Task<PostStatus> PostStatusAsync(Guid postId)
    {
        using var scope = Factory.Services.CreateScope();
        var posts = scope.ServiceProvider.GetRequiredService<ICommunityPostRepository>();

        var post = await posts.GetAsync(CommunityPostId.From(postId));

        return post!.Status;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "the request should have succeeded, and the body says why it did not: {0}", body);

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record SeededCommunity(Guid Id, string Slug);

    private sealed record PostDto(Guid PostId);

    private sealed record FiledReportDto(Guid ReportId);

    // The client's CommunityReport, re-declared by hand so a renamed field fails here.
    private sealed record ReportDto(
        Guid Id,
        Guid CommunityId,
        string Status,
        string Reason,
        string ContentType,
        Guid ContentId,
        string? ContentExcerpt,
        int ReporterCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        string? Note);
}
