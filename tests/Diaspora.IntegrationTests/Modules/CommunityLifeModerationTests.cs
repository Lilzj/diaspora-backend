using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Reports about events and organisations, and what staff may do about them: decide the Trust case,
/// and nothing else (7 Nov 2026).
/// <para>
/// <b>Until stage 5 a moderator closed a report here and named an action alongside it</b>
/// (<c>POST /community-life/reports/{id}/resolution</c>), or acted on a subject directly
/// (<c>POST /community-life/{events|organizations}/{id}/moderation</c>). Both are gone: a report now
/// opens a Trust case, a decision on the case is carried out on the event or organisation by this
/// module, and the case's decision closes the report. The claims the old suite made still hold and
/// are asserted through the case: a word the platform cannot read is refused rather than ignored, a
/// refusal leaves the report open, a decision that is read takes effect, closing a report with no
/// action leaves the subject alone, and an ordinary member cannot decide anything.
/// </para>
/// <para>
/// The wire shapes are re-declared here rather than referenced from the module, for the reason the
/// neighbouring suites give: a contract test bound to the module's own records passes whenever the
/// module compiles.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityLifeModerationTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/community-life";
    private const string Trust = "/api/v1/trust";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityLifeModerationTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Events: the case loop ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_report_about_an_event_opens_a_case_naming_its_organiser()
    {
        var subject = await PublishedEventAsync();
        await ReportAsync("events", subject.Id, "Fraud", "Collecting deposits for a hall nobody booked.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");

        var item = await CaseForAsync("event", subject.Id);
        item.TargetType.Should().Be("event");
        item.Area.Should().Be("events");
        item.ReasonCode.Should().Be("FraudOrScam", "Fraud maps to the platform's fraud or scam");
        item.ReportCount.Should().Be(1);
        item.AuthorId.Should().Be(subject.OwnerId, "the organiser answers for the event");

        var queue = await ReadDataAsync<PageShape<CaseShape>>(
            await HttpClient.GetAsync($"{Trust}/moderation/queue?targetType=event&pageSize=100"));
        queue.Items.Should().Contain(entry => entry.ItemId == item.ItemId, "a fraud report lists the case at once");
    }

    [Fact]
    public async Task A_hide_through_the_case_takes_the_event_down_and_a_restore_brings_it_back()
    {
        var subject = await PublishedEventAsync();
        var report = await ReportAsync("events", subject.Id, "Fraud", "Collecting deposits for a hall nobody booked.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync("event", subject.Id);
        await AssignAsync(item.ItemId);

        var hidden = await DecideAsync(item.ItemId, "HidePendingReview", "FraudOrScam",
            "Takes deposits for a venue that was never booked.");
        hidden.StatusCode.Should().Be(HttpStatusCode.OK, await hidden.Content.ReadAsStringAsync());

        (await AnonymousStatusAsync($"{Root}/events/{subject.Id}"))
            .Should().Be(HttpStatusCode.NotFound, "community life carried the hide out");

        var asStaff = await ReadDataAsync<EventShape>(await HttpClient.GetAsync($"{Root}/events/{subject.Id}"));
        asStaff.ModerationState.Should().Be("Hidden");
        asStaff.Status.Should().Be("Published", "a hide never cancels the event and tells its attendees so");

        await Factory.DrainOutboxAsync();

        (await ReportsAsync()).Should().Contain(
            entry => entry.ReportId == report.ReportId && entry.Status == "Upheld",
            "the decision closed the module's report");

        // The organiser fixes it and asks for another look; a moderator restores it.
        Api.Authenticate(subject.OwnerToken);
        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the organiser is the case's owner");

        await SignInAsRoleAsync("Moderator");
        await AssignAsync(item.ItemId);

        var restored = await DecideAsync(item.ItemId, "Restore", "Other",
            "The organiser showed the hall booking and the deposit receipts.");
        restored.StatusCode.Should().Be(HttpStatusCode.OK, await restored.Content.ReadAsStringAsync());

        (await AnonymousStatusAsync($"{Root}/events/{subject.Id}")).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_removal_of_an_event_takes_effect_only_when_a_second_member_of_staff_confirms_it()
    {
        var subject = await PublishedEventAsync();
        await ReportAsync("events", subject.Id, "Fraud", "Tickets for a concert the band never agreed to.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync("event", subject.Id);
        await AssignAsync(item.ItemId);

        var proposed = await DecideAsync(item.ItemId, "Remove", "FraudOrScam",
            "Sells tickets for a concert the band has never heard of.");
        proposed.StatusCode.Should().Be(HttpStatusCode.OK, await proposed.Content.ReadAsStringAsync());
        (await ReadDataAsync<CaseShape>(proposed)).CurrentOutcome.Should().Be("HidePendingReview");

        (await AnonymousStatusAsync($"{Root}/events/{subject.Id}"))
            .Should().Be(HttpStatusCode.NotFound, "hidden while the removal waits for a second look");

        await SignInAsRoleAsync("Moderator");

        var confirmed = await SecondReviewAsync(item.ItemId, confirm: true,
            "Checked with the band's agent: no such concert.");
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());
        (await ReadDataAsync<CaseShape>(confirmed)).CurrentOutcome.Should().Be("Remove");

        (await AnonymousStatusAsync($"{Root}/events/{subject.Id}")).Should().Be(HttpStatusCode.NotFound);

        var asStaff = await ReadDataAsync<EventShape>(await HttpClient.GetAsync($"{Root}/events/{subject.Id}"));
        asStaff.ModerationState.Should().Be("Suspended", "a removal bars republication until staff lift it");
        asStaff.Status.Should().Be("Published", "a removal never cancels the event either");

        // The organiser still reaches their own event, and sees why it is down.
        Api.Authenticate(subject.OwnerToken);
        (await ReadDataAsync<EventShape>(await HttpClient.GetAsync($"{Root}/events/{subject.Id}")))
            .ModerationState.Should().Be("Suspended");
    }

    // ── Organisations: the case loop ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_report_about_an_organisation_opens_a_case_naming_its_owner_member_not_its_creator()
    {
        var subject = await PublishedOrganizationAsync();

        // The creator hands the body on and stands down to administrator. From here the person who
        // answers for it is the new owner, which the creator's id no longer says.
        var (_, successor) = await RegisterVerifiedMemberAsync();
        await HandOverOwnershipAsync(subject, successor);

        await ReportAsync("organizations", subject.Id, "Fraud", "Collects dues for a union that was dissolved.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");

        var item = await CaseForAsync("organization", subject.Id);
        item.TargetType.Should().Be("organization");
        item.Area.Should().Be("events");
        item.AuthorId.Should().Be(successor.User.Id, "the Owner member answers for the page, not its creator");
        item.AuthorId.Should().NotBe(subject.OwnerId);
    }

    [Fact]
    public async Task A_hide_through_the_case_takes_the_organisation_down_and_a_restore_brings_it_back()
    {
        var subject = await PublishedOrganizationAsync();
        var report = await ReportAsync("organizations", subject.Id, "Fraud", "Collects dues for a union that was dissolved.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync("organization", subject.Id);
        await AssignAsync(item.ItemId);

        var hidden = await DecideAsync(item.ItemId, "HidePendingReview", "FraudOrScam",
            "Collects membership dues for a body that no longer exists.");
        hidden.StatusCode.Should().Be(HttpStatusCode.OK, await hidden.Content.ReadAsStringAsync());

        (await AnonymousStatusAsync($"{Root}/organizations/{subject.Id}"))
            .Should().Be(HttpStatusCode.NotFound, "community life carried the hide out");

        var asStaff = await ReadDataAsync<OrganizationShape>(
            await HttpClient.GetAsync($"{Root}/organizations/{subject.Id}"));
        asStaff.ModerationState.Should().Be("Hidden");
        asStaff.Status.Should().Be("Published", "a hide is not an archive");

        await Factory.DrainOutboxAsync();

        (await ReportsAsync()).Should().Contain(
            entry => entry.ReportId == report.ReportId && entry.Status == "Upheld");

        // A case is decided once; a change comes from the owner's review request. Without one, a
        // second decision is refused.
        (await DecideAsync(item.ItemId, "Restore", "Other", "Second thoughts, without a request."))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        Api.Authenticate(subject.OwnerToken);
        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await SignInAsRoleAsync("Moderator");
        await AssignAsync(item.ItemId);

        var restored = await DecideAsync(item.ItemId, "Restore", "Other",
            "The union was re-registered last month; the dues are genuine.");
        restored.StatusCode.Should().Be(HttpStatusCode.OK, await restored.Content.ReadAsStringAsync());

        (await AnonymousStatusAsync($"{Root}/organizations/{subject.Id}")).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_removal_of_an_organisation_takes_effect_only_when_a_second_member_of_staff_confirms_it()
    {
        var subject = await PublishedOrganizationAsync();
        await ReportAsync("organizations", subject.Id, "Fraud", "Uses a registered charity's name and number.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync("organization", subject.Id);
        await AssignAsync(item.ItemId);

        var proposed = await DecideAsync(item.ItemId, "Remove", "Impersonation",
            "Uses a registered charity's name and number to collect money.");
        proposed.StatusCode.Should().Be(HttpStatusCode.OK, await proposed.Content.ReadAsStringAsync());

        (await SecondReviewAsync(item.ItemId, confirm: true, "Agreeing with my own proposal, obviously."))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "nobody checks their own decision");

        await SignInAsRoleAsync("Moderator");

        var confirmed = await SecondReviewAsync(item.ItemId, confirm: true,
            "The charity confirms it has no such branch.");
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());
        (await ReadDataAsync<CaseShape>(confirmed)).CurrentOutcome.Should().Be("Remove");

        (await AnonymousStatusAsync($"{Root}/organizations/{subject.Id}")).Should().Be(HttpStatusCode.NotFound);

        var asStaff = await ReadDataAsync<OrganizationShape>(
            await HttpClient.GetAsync($"{Root}/organizations/{subject.Id}"));
        asStaff.ModerationState.Should().Be("Suspended");
        asStaff.Status.Should().Be("Suspended", "a suspended body's events stop appearing too");
    }

    // ── What the old suite asserted, through the case ───────────────────────────────────────

    /// <summary>
    /// The defect the old suite was written for: a word the platform cannot read must be refused,
    /// not answered with success. Through the case, an unknown outcome is a 400 and changes nothing.
    /// </summary>
    [Fact]
    public async Task An_outcome_the_platform_cannot_read_is_refused_and_leaves_the_report_open()
    {
        var subject = await PublishedEventAsync();
        var report = await ReportAsync("events", subject.Id, "Fraud", "Collecting deposits for a hall nobody booked.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync("event", subject.Id);
        await AssignAsync(item.ItemId);

        var response = await DecideAsync(item.ItemId, "Suspend", "FraudOrScam", "Not an outcome the platform has.");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await AnonymousStatusAsync($"{Root}/events/{subject.Id}"))
            .Should().Be(HttpStatusCode.OK, "a refused decision took nothing down");

        await Factory.DrainOutboxAsync();

        (await ReportsAsync()).Should().Contain(
            entry => entry.ReportId == report.ReportId && entry.Status == "Open",
            "the report is still open for somebody to decide properly");
    }

    /// <summary>
    /// Closing a report without touching its subject is the commonest decision there is: no action
    /// dismisses the report, clears the automatic flag the fraud report put on, and nothing else.
    /// </summary>
    [Fact]
    public async Task No_action_dismisses_the_report_and_leaves_the_event_as_it_was()
    {
        var subject = await PublishedEventAsync();
        var report = await ReportAsync("events", subject.Id, "Fraud", "Collecting deposits for a hall nobody booked.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");

        (await ReadDataAsync<EventShape>(await HttpClient.GetAsync($"{Root}/events/{subject.Id}")))
            .ModerationState.Should().Be("Flagged", "a fraud report flags on the first report");

        var item = await CaseForAsync("event", subject.Id);
        await AssignAsync(item.ItemId);

        var decided = await DecideAsync(item.ItemId, "NoAction", "Other",
            "The hall booking is real; the deposit is the venue's own policy.");
        decided.StatusCode.Should().Be(HttpStatusCode.OK, await decided.Content.ReadAsStringAsync());

        (await AnonymousStatusAsync($"{Root}/events/{subject.Id}")).Should().Be(HttpStatusCode.OK);

        var asStaff = await ReadDataAsync<EventShape>(await HttpClient.GetAsync($"{Root}/events/{subject.Id}"));
        asStaff.ModerationState.Should().Be("Clear", "no action takes the automatic flag off, nothing more");
        asStaff.Status.Should().Be("Published");

        await Factory.DrainOutboxAsync();

        (await ReportsAsync()).Should().Contain(
            entry => entry.ReportId == report.ReportId && entry.Status == "Dismissed");
    }

    /// <summary>
    /// An ordinary member cannot decide a case, however verified they are.
    /// <para>
    /// Signed in for real rather than with an assembled token: a token minted in the test process
    /// carries a session that does not exist and is refused at the door, which would make this pass
    /// without ever reaching the gate it claims to be about.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_ordinary_member_cannot_decide_a_case()
    {
        var subject = await PublishedEventAsync();
        await ReportAsync("events", subject.Id, "Fraud", "Collecting deposits for a hall nobody booked.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync("event", subject.Id);

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        (await DecideAsync(item.ItemId, "NoAction", "Other", "Nothing wrong with it at all."))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The module's own staff actions are gone. Only the report filing and the report list remain.
    /// </summary>
    [Fact]
    public async Task The_modules_own_moderation_and_resolution_routes_are_gone()
    {
        var subject = await PublishedEventAsync();
        var report = await ReportAsync("events", subject.Id, "Fraud", "Collecting deposits for a hall nobody booked.");

        await SignInAsRoleAsync("Moderator");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/events/{subject.Id}/moderation", new { action = "Hidden", reason = "The old way." }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/reports/{report.ReportId}/resolution",
                new { outcome = "Upheld", note = "The old way.", action = "Suspended" }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);

        (await AnonymousStatusAsync($"{Root}/events/{subject.Id}"))
            .Should().Be(HttpStatusCode.OK, "nothing acted on the event");

        (await ReportsAsync()).Should().Contain(entry => entry.ReportId == report.ReportId && entry.Status == "Open");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private sealed record Subject(Guid Id, Guid OwnerId, string OwnerToken);

    /// <summary>An event a fresh verified member created and published.</summary>
    private async Task<Subject> PublishedEventAsync()
    {
        var (_, organizer) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(organizer.Tokens.AccessToken);

        var created = await ReadDataAsync<EventShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/events",
                new
                {
                    title = $"Test Gathering {Guid.NewGuid():N}",
                    summary = "An event created by the integration suite.",
                    categoryKey = "community-meeting",
                    startsAt = DateTimeOffset.UtcNow.AddDays(14),
                    endsAt = DateTimeOffset.UtcNow.AddDays(14).AddHours(3),
                    countryCode = "GB",
                    cityName = "London",
                    venue = new { name = "Community Hall", addressLine1 = "1 High Street" }
                }));

        // Creating an event only drafts it, and a draft is a 404 to everybody but its organizer —
        // which would make every assertion about an anonymous reader pass without moderation having
        // done anything at all.
        (await HttpClient.PostAsJsonAsync($"{Root}/events/{created.EventId}/publish", new { }))
            .EnsureSuccessStatusCode();

        (await AnonymousStatusAsync($"{Root}/events/{created.EventId}")).Should().Be(HttpStatusCode.OK);

        return new Subject(created.EventId, organizer.User.Id, organizer.Tokens.AccessToken);
    }

    /// <summary>An organisation a fresh verified member created and published.</summary>
    private async Task<Subject> PublishedOrganizationAsync()
    {
        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var created = await ReadDataAsync<OrganizationShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/organizations",
                new
                {
                    name = $"Test Association {Guid.NewGuid():N}",
                    summary = "A body created by the integration suite.",
                    organizationTypeKey = "cultural-association",
                    countryCode = "GB",
                    cityName = "London"
                }));

        var published = await HttpClient.PostAsJsonAsync(
            $"{Root}/organizations/{created.OrganizationId}/publish", new { });
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        (await AnonymousStatusAsync($"{Root}/organizations/{created.OrganizationId}"))
            .Should().Be(HttpStatusCode.OK);

        return new Subject(created.OrganizationId, owner.User.Id, owner.Tokens.AccessToken);
    }

    /// <summary>
    /// The creator invites the successor as an owner, the successor accepts, and the creator stands
    /// down to administrator, so exactly one Owner member remains and it is not the creator.
    /// </summary>
    private async Task HandOverOwnershipAsync(Subject organization, AuthenticationResponse successor)
    {
        Api.Authenticate(organization.OwnerToken);

        var invitation = await ReadDataAsync<MembershipShape>(await HttpClient.PostAsJsonAsync(
            $"{Root}/organizations/{organization.Id}/members/invitations",
            new { memberUserId = successor.User.Id, role = "Owner" }));

        Api.Authenticate(successor.Tokens.AccessToken);

        var accepted = await HttpClient.PostAsJsonAsync(
            $"{Root}/organizations/{organization.Id}/members/{invitation.MembershipId}/decision",
            new { approve = true });
        accepted.IsSuccessStatusCode.Should().BeTrue(await accepted.Content.ReadAsStringAsync());

        Api.Authenticate(organization.OwnerToken);

        var members = await ReadDataAsync<PageShape<MembershipShape>>(
            await HttpClient.GetAsync($"{Root}/organizations/{organization.Id}/members"));
        var creator = members.Items.Single(member => member.MemberUserId == organization.OwnerId);

        var steppedDown = await HttpClient.PutAsJsonAsync(
            $"{Root}/organizations/{organization.Id}/members/{creator.MembershipId}/role",
            new { role = "Administrator" });
        steppedDown.IsSuccessStatusCode.Should().BeTrue(await steppedDown.Content.ReadAsStringAsync());
    }

    /// <summary>A fresh member reports the subject; the caller is left signed in as them.</summary>
    private async Task<ReportShape> ReportAsync(string segment, Guid subjectId, string reason, string detail)
    {
        var (_, reporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporter.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/{segment}/{subjectId}/reports", new { reason, detail });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return await ReadDataAsync<ReportShape>(response);
    }

    /// <summary>
    /// The module's report list, as the signed-in moderator reads it, in every status: the list
    /// shows only open and under-review reports unless asked, and a closed report is what these
    /// tests look for. Newest deception reports come first, so the ones a test just filed are on
    /// this page however many other tests have filed before it.
    /// </summary>
    private async Task<List<ReportShape>> ReportsAsync() =>
        (await ReadDataAsync<PageShape<ReportShape>>(
            await HttpClient.GetAsync(
                $"{Root}/reports?limit=100&status=Open&status=UnderReview&status=Upheld&status=Dismissed&status=Duplicate"))).Items;

    private async Task<HttpStatusCode> AnonymousStatusAsync(string path)
    {
        using var anonymous = Factory.CreateClient();

        return (await anonymous.GetAsync(path)).StatusCode;
    }

    private async Task<CaseShape> CaseForAsync(string contentType, Guid contentId) =>
        await ReadDataAsync<CaseShape>(
            await HttpClient.GetAsync($"{Trust}/moderation/cases?contentType={contentType}&contentId={contentId}"));

    private async Task AssignAsync(Guid itemId) =>
        (await HttpClient.PostAsync($"{Trust}/moderation/queue/{itemId}/assign", content: null))
            .EnsureSuccessStatusCode();

    private Task<HttpResponseMessage> DecideAsync(Guid itemId, string outcome, string reasonCode, string note) =>
        HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{itemId}/decision", new { outcome, reasonCode, note });

    private Task<HttpResponseMessage> SecondReviewAsync(Guid itemId, bool confirm, string note) =>
        HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{itemId}/second-review", new { confirm, note });

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} answered {(int)response.StatusCode}: {body}");

        var envelope = JsonSerializer.Deserialize<Envelope<T>>(body, JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record PageShape<T>(List<T> Items);

    private sealed record EventShape(Guid EventId, string Status, string ModerationState);

    private sealed record OrganizationShape(Guid OrganizationId, string Status, string ModerationState);

    private sealed record MembershipShape(Guid MembershipId, Guid MemberUserId, string Role);

    private sealed record ReportShape(Guid ReportId, string SubjectType, Guid SubjectId, string Status);

    private sealed record CaseShape(
        Guid ItemId,
        string ContentId,
        Guid AuthorId,
        string? TargetType,
        string? Area,
        string? ReasonCode,
        int ReportCount,
        string? CurrentOutcome);
}
