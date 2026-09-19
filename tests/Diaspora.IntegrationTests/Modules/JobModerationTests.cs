using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Reporting a job advert, and what a moderator does about it.
/// <para>
/// <b>Until this shipped, jobs was the only vertical on the platform with no way to report
/// anything.</b> Housing, marketplace, guides, community and community life all had the loop;
/// jobs had the permissions — <c>jobs:report</c> and <c>jobs:moderate</c> — declared against no
/// route, and a posting aggregate carrying <c>Hide</c> and <c>Flag</c> that nothing could call.
/// A member who found a fraudulent advert had nowhere to say so.
/// </para>
/// <para>
/// So the first test is about reachability, and the rest are about the one rule that makes this
/// queue worth having: <b>an advert demanding money from a candidate flags on the first report</b>,
/// where everything else waits for a second. Recruitment fraud takes from somebody who has just
/// arrived and has nobody to ask, and waiting for a second reporter is a day that costs them.
/// </para>
/// </summary>
public sealed class JobModerationTests : IdentityTestBase
{
    private const string Root = "/api/v1/jobs";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JobModerationTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>The whole point: a member can report, and a moderator sees it.</summary>
    [Fact]
    public async Task A_reported_advert_reaches_the_moderation_queue()
    {
        var posting = await PublishedPostingAsync("Warehouse Operative");

        await ReportAsAnotherMemberAsync(posting, "Misleading", "The pay is not what it says.");

        await SignInAsRoleAsync("Moderator");

        var queue = await ReadDataAsync<List<ReportRow>>(
            await HttpClient.GetAsync($"{Root}/moderation/reports"));

        var row = queue.Should().ContainSingle(entry => entry.PostingId == posting).Subject;

        row.Reason.Should().Be("Misleading");
        row.Detail.Should().Be("The pay is not what it says.");
        row.Status.Should().Be("Open");
        row.PostingTitle.Should().Be("Warehouse Operative");
    }

    /// <summary>
    /// The rule this queue exists for. One report of a fee demand flags the advert; nobody has to
    /// find a second person who was asked for money first.
    /// </summary>
    [Fact]
    public async Task A_demand_for_money_flags_the_advert_on_the_first_report()
    {
        var posting = await PublishedPostingAsync("Care Assistant — Visa Provided");

        await ReportAsAnotherMemberAsync(
            posting, "FeeRequested", "They asked for 400 euros for the visa paperwork.");

        await SignInAsRoleAsync("Moderator");

        var queue = await ReadDataAsync<List<ReportRow>>(
            await HttpClient.GetAsync($"{Root}/moderation/reports"));

        queue.Single(entry => entry.PostingId == posting)
            .PostingModerationState.Should().Be("Flagged");
    }

    /// <summary>
    /// And an ordinary complaint does not, because one disgruntled applicant is not evidence and
    /// a queue that flagged on every grumble would be a queue nobody works.
    /// </summary>
    [Fact]
    public async Task An_ordinary_complaint_does_not_flag_on_the_first_report()
    {
        var posting = await PublishedPostingAsync("Delivery Driver");

        await ReportAsAnotherMemberAsync(posting, "AlreadyFilled", "I was told it is gone.");

        await SignInAsRoleAsync("Moderator");

        var queue = await ReadDataAsync<List<ReportRow>>(
            await HttpClient.GetAsync($"{Root}/moderation/reports"));

        queue.Single(entry => entry.PostingId == posting)
            .PostingModerationState.Should().Be("Clear");
    }

    /// <summary>
    /// The advert still comes down when a moderator says so, and an anonymous reader can no
    /// longer find it. Hiding is the decision; flagging never was.
    /// </summary>
    [Fact]
    public async Task Upholding_with_a_takedown_removes_the_advert_from_public_search()
    {
        var posting = await PublishedPostingAsync("Kitchen Porter");

        var reportId = await ReportAsAnotherMemberAsync(
            posting, "Scam", "There is no restaurant at that address.");

        (await IsPubliclyFindableAsync(posting)).Should().BeTrue("it was published a moment ago");

        await SignInAsRoleAsync("Moderator");

        var resolved = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/reports/{reportId}/resolve",
            new { upheld = true, resolution = "No such business.", hidePosting = true });

        resolved.StatusCode.Should().Be(HttpStatusCode.OK);

        (await IsPubliclyFindableAsync(posting)).Should().BeFalse();
    }

    /// <summary>
    /// Dismissing clears the flag. An advert looked at and found to be nothing must go back into
    /// discovery rather than carrying a mark that quietly penalises the employer.
    /// </summary>
    [Fact]
    public async Task Dismissing_clears_the_flag_the_report_raised()
    {
        var posting = await PublishedPostingAsync("Night Security Officer");

        var reportId = await ReportAsAnotherMemberAsync(
            posting, "Scam", "Looks fake to me.");

        await SignInAsRoleAsync("Moderator");

        await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/reports/{reportId}/resolve",
            new { upheld = false, resolution = "Checked the company register. It is real." });

        var queue = await ReadDataAsync<List<ReportRow>>(
            await HttpClient.GetAsync($"{Root}/moderation/reports?openOnly=false"));

        var row = queue.Single(entry => entry.ReportId == reportId);

        row.Status.Should().Be("Dismissed");
        row.PostingModerationState.Should().Be("Clear");
    }

    /// <summary>
    /// One decision closes every open report about the same advert. Otherwise the same posting
    /// returns to the top of the queue once per reporter.
    /// </summary>
    [Fact]
    public async Task One_decision_closes_every_open_report_about_the_same_advert()
    {
        var posting = await PublishedPostingAsync("Packing Line Operative");

        var first = await ReportAsAnotherMemberAsync(posting, "Misleading", "Hours are wrong.");
        var second = await ReportAsAnotherMemberAsync(posting, "AlreadyFilled", "It is taken.");

        await SignInAsRoleAsync("Moderator");

        await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/reports/{first}/resolve",
            new { upheld = true, resolution = "Advert corrected." });

        var queue = await ReadDataAsync<List<ReportRow>>(
            await HttpClient.GetAsync($"{Root}/moderation/reports?openOnly=false"));

        queue.Single(entry => entry.ReportId == first).Status.Should().Be("Upheld");
        queue.Single(entry => entry.ReportId == second).Status.Should().Be("Upheld");
    }

    /// <summary>
    /// An employer cannot report their own advert. The report count is what flags a posting, so a
    /// seat that could inflate its own would be a way to bury a rival.
    /// </summary>
    [Fact]
    public async Task An_employer_cannot_report_their_own_advert()
    {
        var (posting, _) = await PublishedPostingAsOwnerAsync("Site Supervisor");

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/postings/{posting}/reports",
            new { reason = "Scam", detail = "Reporting myself." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// One member, one report. The threshold counts people who think something is wrong, and a
    /// member who could report five times would decide that alone.
    /// </summary>
    [Fact]
    public async Task A_member_cannot_report_the_same_advert_twice()
    {
        var posting = await PublishedPostingAsync("Barista");

        var (_, reporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporter.Tokens.AccessToken);

        var first = await HttpClient.PostAsJsonAsync(
            $"{Root}/postings/{posting}/reports", new { reason = "Offensive", detail = "Rude." });

        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await HttpClient.PostAsJsonAsync(
            $"{Root}/postings/{posting}/reports", new { reason = "Scam", detail = "And fake." });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>An ordinary member cannot read other people's reports.</summary>
    [Fact]
    public async Task An_ordinary_member_cannot_read_the_moderation_queue()
    {
        var posting = await PublishedPostingAsync("Cleaner");

        await ReportAsAnotherMemberAsync(posting, "Misleading", "Not as described.");

        var response = await HttpClient.GetAsync($"{Root}/moderation/reports");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A moderation verb the module cannot read is refused rather than ignored. Answering an
    /// unknown word with success is how a moderator comes to believe they hid an advert they did
    /// not — the defect this platform has already had once, in community life.
    /// </summary>
    [Fact]
    public async Task An_action_the_module_cannot_read_is_refused()
    {
        var posting = await PublishedPostingAsync("Forklift Driver");

        await SignInAsRoleAsync("Moderator");

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/postings/{posting}",
            new { action = "Suspend", reason = "Not a verb this module has." });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await IsPubliclyFindableAsync(posting)).Should().BeTrue(
            "a refused action must not half-apply");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A published advert. The caller is left signed in as its employer.</summary>
    private async Task<(Guid PostingId, Guid EmployerId)> PublishedPostingAsOwnerAsync(string title)
    {
        Factory.EnsureDatabaseCreated();

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var employer = await ReadDataAsync<EmployerShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/employers",
                new
                {
                    displayName = $"Test Employer {Guid.NewGuid():N}",
                    organizationType = "Healthcare",
                    countryCode = "DE"
                }));

        var created = await HttpClient.PostAsJsonAsync(
            $"{Root}/",
            new
            {
                employerId = employer.EmployerId,
                title,
                description = "A posting created by the integration suite for moderation tests.",
                categoryKey = "healthcare",
                jobType = "FullTime",
                experienceLevel = "Mid",
                countryCode = "DE",
                cityName = "Berlin",
                workplaceType = "OnSite"
            });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var job = await ReadDataAsync<JobShape>(created);

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/{job.JobId}/publish", new { openImmediately = true }))
            .EnsureSuccessStatusCode();

        return (job.JobId, employer.EmployerId);
    }

    private async Task<Guid> PublishedPostingAsync(string title) =>
        (await PublishedPostingAsOwnerAsync(title)).PostingId;

    /// <summary>Somebody who is not the employer reporting the advert.</summary>
    private async Task<Guid> ReportAsAnotherMemberAsync(Guid postingId, string reason, string detail)
    {
        var (_, reporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporter.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/postings/{postingId}/reports", new { reason, detail });

        response.StatusCode.Should().Be(
            HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return await ReadDataAsync<Guid>(response);
    }

    /// <summary>Whether an anonymous reader can still find the advert.</summary>
    private async Task<bool> IsPubliclyFindableAsync(Guid postingId)
    {
        using var anonymous = Factory.CreateClient();

        var found = await ReadDataAsync<SearchShape>(
            await anonymous.PostAsJsonAsync(
                $"{Root}/search", new { countryCode = "DE", openOnly = true, limit = 100 }));

        return found.Items.Any(item => item.JobId == postingId);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record EmployerShape(Guid EmployerId);

    private sealed record JobShape(Guid JobId, string Status);

    private sealed record SearchShape(List<SearchItem> Items);

    private sealed record SearchItem(Guid JobId);

    private sealed record ReportRow(
        Guid ReportId,
        Guid PostingId,
        string PostingTitle,
        string Reason,
        string? Detail,
        string Status,
        int EmployerUpheldReports,
        int TotalReportsOnPosting,
        string PostingModerationState);
}
