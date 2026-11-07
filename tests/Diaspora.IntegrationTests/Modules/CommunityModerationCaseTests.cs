using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Community posts and comments on Trust's one moderation case loop (7 Nov 2026).
/// <para>
/// A report filed through <c>POST /community/reports</c> opens or joins the Trust case for that
/// content; staff decide only on the case; Community carries the decision out on the post or the
/// comment; a removal needs a second member of staff; and the Community report rows close when a
/// person decides. Community's own staff decision route and its tenth-report hide are gone: the
/// automatic hide on many serious reports is Trust's now, and it is asserted here through the case.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityModerationCaseTests : PooledIdentityTestBase
{
    private const string Community = "/api/v1/community";
    private const string Trust = "/api/v1/trust";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityModerationCaseTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Posts ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_report_on_a_post_opens_a_case_and_a_hide_through_trust_takes_it_out_of_view_until_restored()
    {
        var post = await PublishedPostAsync("Cheap residence permits, message me for the price list.");
        var reader = await ReaderTokenAsync();

        await ReportAsync("post", post.PostId, "harassment", "Abusive replies to everyone who asked.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");

        var item = await CaseForAsync("community-post", post.PostId);
        item.ReasonCode.Should().Be("OffensiveOrHarassment", "Community's harassment is the platform's");
        item.ReportCount.Should().Be(1);

        await AssignAsync(item.ItemId);

        var hidden = await DecideAsync(item.ItemId, "HidePendingReview", "OffensiveOrHarassment",
            "Insults members in every reply under the post.");
        hidden.StatusCode.Should().Be(HttpStatusCode.OK, await hidden.Content.ReadAsStringAsync());

        (await ReadPostAsAsync(reader, post.PostId)).Should().Be(
            HttpStatusCode.NotFound, "Community carried the hide out, and the permalink honours it");

        Api.Authenticate(post.OwnerToken);
        (await HttpClient.GetAsync($"{Community}/posts/{post.PostId}")).StatusCode.Should().Be(
            HttpStatusCode.OK, "the author can still read what was hidden, to fix it or ask for a review");

        await Factory.DrainOutboxAsync();

        (await ReportStatusesAsync(CommunityContentType.Post, post.PostId))
            .Should().ContainSingle()
            .Which.Should().Be(ReportStatus.Resolved, "the decision upheld Community's report row");

        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await SignInAsRoleAsync("Moderator");
        await AssignAsync(item.ItemId);

        var restored = await DecideAsync(item.ItemId, "Restore", "Other",
            "The author removed the abusive line and apologised.");
        restored.StatusCode.Should().Be(HttpStatusCode.OK, await restored.Content.ReadAsStringAsync());

        (await ReadPostAsAsync(reader, post.PostId)).Should().Be(HttpStatusCode.OK, "a restore puts it back");
    }

    [Fact]
    public async Task A_post_removal_takes_effect_only_when_a_second_member_of_staff_confirms_it()
    {
        var post = await PublishedPostAsync("Send me your passport scan and I will get you a visa.");
        var reader = await ReaderTokenAsync();

        await ReportAsync("post", post.PostId, "personalData", "Collecting passport scans from strangers.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync("community-post", post.PostId);
        await AssignAsync(item.ItemId);

        var proposed = await DecideAsync(item.ItemId, "Remove", "PersonalData",
            "Asks members to post passport scans to a private chat.");
        proposed.StatusCode.Should().Be(HttpStatusCode.OK, await proposed.Content.ReadAsStringAsync());

        var pending = await ReadDataAsync<CaseShape>(proposed);
        pending.CurrentOutcome.Should().Be("HidePendingReview", "until it is confirmed, it is only hidden");
        (await PostStatusAsync(post.PostId)).Should().Be(PostStatus.Hidden);

        await SignInAsRoleAsync("Moderator");

        var confirmed = await SecondReviewAsync(item.ItemId, "Checked the post and the report. Agreed.");
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());

        (await ReadDataAsync<CaseShape>(confirmed)).CurrentOutcome.Should().Be("Remove");

        (await PostStatusAsync(post.PostId)).Should().Be(
            PostStatus.Removed, "a confirmed removal uses the state only a restore lifts — and is not a deletion");
        (await ReadPostAsAsync(reader, post.PostId)).Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// <b>Ten serious reports hide a post as the system, through Trust (7 Nov 2026).</b> The same
    /// number used to hide it inside Community, with a stock reason, no case and nothing the author
    /// could read. Now the case records the hide, nobody's report is resolved until a person looks,
    /// and the post is back on a moderator's queue.
    /// </summary>
    [Fact]
    public async Task Ten_serious_reports_hide_a_post_pending_review_through_its_case()
    {
        var post = await PublishedPostAsync("You people should all go back where you came from.");
        var reader = await ReaderTokenAsync();

        for (var reporter = 0; reporter < 10; reporter++)
        {
            await ReportAsync("post", post.PostId, "harassment", $"Hateful post, report {reporter + 1}.");
        }

        (await PostStatusAsync(post.PostId)).Should().Be(
            PostStatus.Published, "Community no longer hides anything on its own, however many reports");

        await Factory.DrainOutboxAsync();

        (await PostStatusAsync(post.PostId)).Should().Be(PostStatus.Hidden, "Trust hid it pending review");
        (await ReadPostAsAsync(reader, post.PostId)).Should().Be(HttpStatusCode.NotFound);

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync("community-post", post.PostId);
        item.CurrentOutcome.Should().Be("HidePendingReview");
        item.ReportCount.Should().Be(10);

        await Factory.DrainOutboxAsync();

        (await ReportStatusesAsync(CommunityContentType.Post, post.PostId)).Should().OnlyContain(
            status => status == ReportStatus.Open, "no person has looked yet, so no report is resolved");
    }

    // ── Comments ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_report_on_a_comment_opens_a_case_that_hides_restores_and_removes_it()
    {
        var post = await PublishedPostAsync("Road closed at the market roundabout this morning.");

        // Registering a member signs the shared client out, so the reader is made before anyone
        // signs in, and reads through a client of its own.
        var reader = await ReaderTokenAsync();

        var (_, commenter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(commenter.Tokens.AccessToken);

        const string Abuse = "Nobody asked you, go home.";

        var comment = await ReadDataAsync<CommentShape>(await HttpClient.PostAsJsonAsync(
            $"{Community}/posts/{post.PostId}/comments", new { body = Abuse }));

        await ReportAsync("comment", comment.CommentId, "harassment", "Telling the poster to go home.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync("community-comment", comment.CommentId);
        await AssignAsync(item.ItemId);

        (await DecideAsync(item.ItemId, "HidePendingReview", "OffensiveOrHarassment",
                "Tells another member to go home; hidden until reviewed."))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await ThreadBodiesAsync(reader, post.PostId)).Should().NotContain(Abuse, "a hidden comment leaves its thread");

        Api.Authenticate(commenter.Tokens.AccessToken);
        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await SignInAsRoleAsync("Moderator");
        await AssignAsync(item.ItemId);

        (await DecideAsync(item.ItemId, "Restore", "Other", "Read in context it was a joke between friends."))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await ThreadBodiesAsync(reader, post.PostId)).Should().Contain(Abuse, "a restore puts it back in the thread");

        // A fresh case, opened by staff with no report, for the removal.
        var opened = await HttpClient.PostAsJsonAsync($"{Trust}/moderation/cases", new
        {
            contentType = "community-comment",
            contentId = comment.CommentId,
            reasonCode = "OffensiveOrHarassment",
            note = "The same member repeated it under three other posts."
        });
        opened.StatusCode.Should().Be(HttpStatusCode.Created, await opened.Content.ReadAsStringAsync());

        var second = await ReadDataAsync<CaseShape>(opened);
        await AssignAsync(second.ItemId);

        (await DecideAsync(second.ItemId, "Remove", "OffensiveOrHarassment",
                "Repeated abuse of other members across threads."))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await SignInAsRoleAsync("Moderator");

        var confirmed = await SecondReviewAsync(second.ItemId, "Read all four comments. Agreed.");
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());
        (await ReadDataAsync<CaseShape>(confirmed)).CurrentOutcome.Should().Be("Remove");

        var thread = await ThreadAsync(reader, post.PostId);
        var removed = thread.Comments.Should().ContainSingle(row => row.CommentId == comment.CommentId).Subject;
        removed.IsTombstoned.Should().BeTrue("a removed comment keeps its place and says it was removed");
        removed.Body.Should().NotBe(Abuse);
    }

    // ── Other modules' content reported through Community ──────────────────────────────────

    [Fact]
    public async Task A_job_reported_through_the_community_modal_lands_on_the_jobs_case()
    {
        Factory.EnsureDatabaseCreated();

        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employer = await ReadDataAsync<EmployerShape>(await HttpClient.PostAsJsonAsync(
            "/api/v1/jobs/employers",
            new { displayName = $"Modal Report Ltd {Guid.NewGuid():N}", organizationType = "Healthcare", countryCode = "DE" }));

        var job = await ReadDataAsync<JobShape>(await HttpClient.PostAsJsonAsync("/api/v1/jobs/", new
        {
            employerId = employer.EmployerId,
            title = "Care Assistant",
            description = "A posting created by the integration suite to be reported through Community.",
            categoryKey = "healthcare",
            jobType = "FullTime",
            experienceLevel = "Mid",
            countryCode = "DE",
            cityName = "Berlin",
            workplaceType = "OnSite"
        }));

        (await HttpClient.PostAsJsonAsync($"/api/v1/jobs/{job.JobId}/publish", new { openImmediately = true }))
            .EnsureSuccessStatusCode();

        await ReportAsync("job", job.JobId, "spam", "The same advert posted ten times today.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");

        var item = await CaseForAsync("job", job.JobId);
        item.ReportCount.Should().Be(1, "the Community report reached the job's case, under the jobs content type");
        item.ReasonCode.Should().Be("Spam");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private sealed record PublishedPost(Guid PostId, string OwnerToken);

    private async Task<PublishedPost> PublishedPostAsync(string body)
    {
        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var post = await ReadDataAsync<PostShape>(await HttpClient.PostAsJsonAsync(
            $"{Community}/posts", new { body, category = "Traffic", urgency = "Normal", publish = true }));

        return new PublishedPost(post.PostId, owner.Tokens.AccessToken);
    }

    /// <summary>An ordinary member, unrelated to the post, who reads it by its permalink.</summary>
    private async Task<string> ReaderTokenAsync()
    {
        var (_, reader) = await RegisterVerifiedMemberAsync();

        return reader.Tokens.AccessToken;
    }

    private async Task<HttpStatusCode> ReadPostAsAsync(string token, Guid postId)
    {
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        return (await client.GetAsync($"{Community}/posts/{postId}")).StatusCode;
    }

    private async Task ReportAsync(string contentType, Guid contentId, string reason, string description)
    {
        var (_, reporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporter.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Community}/reports", new { contentType, contentId, reason, description });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private async Task<ThreadShape> ThreadAsync(string reader, Guid postId)
    {
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", reader);

        return await ReadDataAsync<ThreadShape>(await client.GetAsync($"{Community}/posts/{postId}/comments"));
    }

    private async Task<List<string>> ThreadBodiesAsync(string reader, Guid postId) =>
        (await ThreadAsync(reader, postId)).Comments.Select(comment => comment.Body).ToList();

    private async Task<PostStatus> PostStatusAsync(Guid postId)
    {
        using var scope = Factory.Services.CreateScope();
        var posts = scope.ServiceProvider
            .GetRequiredService<Diaspora.Modules.Community.Domain.Posts.ICommunityPostRepository>();

        var post = await posts.GetAsync(Diaspora.Modules.Community.Domain.Identifiers.CommunityPostId.From(postId));

        return post!.Status;
    }

    private async Task<List<ReportStatus>> ReportStatusesAsync(CommunityContentType type, Guid contentId)
    {
        using var scope = Factory.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportRepository>();

        return (await reports.ListForContentAsync(ContentReference.Create(type, contentId)))
            .Select(report => report.Status)
            .ToList();
    }

    private async Task<CaseShape> CaseForAsync(string contentType, Guid contentId) =>
        await ReadDataAsync<CaseShape>(
            await HttpClient.GetAsync($"{Trust}/moderation/cases?contentType={contentType}&contentId={contentId}"));

    private async Task AssignAsync(Guid itemId) =>
        (await HttpClient.PostAsync($"{Trust}/moderation/queue/{itemId}/assign", content: null))
            .EnsureSuccessStatusCode();

    private Task<HttpResponseMessage> DecideAsync(Guid itemId, string outcome, string reasonCode, string note) =>
        HttpClient.PostAsJsonAsync($"{Trust}/moderation/queue/{itemId}/decision", new { outcome, reasonCode, note });

    private Task<HttpResponseMessage> SecondReviewAsync(Guid itemId, string note) =>
        HttpClient.PostAsJsonAsync($"{Trust}/moderation/queue/{itemId}/second-review", new { confirm = true, note });

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "{0} {1} answered {2}: {3}",
            response.RequestMessage?.Method, response.RequestMessage?.RequestUri, (int)response.StatusCode, body);

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record PostShape(Guid PostId);

    private sealed record CommentShape(Guid CommentId, string Body, bool IsTombstoned);

    private sealed record ThreadShape(List<CommentShape> Comments);

    private sealed record EmployerShape(Guid EmployerId);

    private sealed record JobShape(Guid JobId);

    private sealed record CaseShape(Guid ItemId, string? ReasonCode, int ReportCount, string? CurrentOutcome);
}
