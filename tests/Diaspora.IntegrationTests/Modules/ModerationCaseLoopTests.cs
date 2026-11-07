using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// One moderation case loop for members' content, end to end on a job advert (7 Nov 2026).
/// <para>
/// Reports from the jobs module reach a Trust case; staff decide only on the case; the decision is
/// carried out on the job by the jobs module; a removal needs a second member of staff; the same
/// person may not do both; a case is decided once; and the owner can ask for a review.
/// </para>
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class ModerationCaseLoopTests : PooledIdentityTestBase
{
    private const string Jobs = "/api/v1/jobs";
    private const string Trust = "/api/v1/trust";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ModerationCaseLoopTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task One_report_of_a_fee_demand_opens_a_listed_case_and_a_single_misleading_report_does_not()
    {
        var feeJob = await PublishedJobAsync("Care Assistant, visa included");
        var otherJob = await PublishedJobAsync("Barista");

        await ReportAsync(feeJob.JobId, "FeeRequested", "They asked for 400 euros up front.");
        await ReportAsync(otherJob.JobId, "Misleading", "The hours are not what it says.");
        await Factory.DrainOutboxAsync();

        var (_, moderator) = await SignInAsRoleAsync("Moderator");

        var feeCase = await CaseForAsync(feeJob.JobId);
        feeCase.ReasonCode.Should().Be("AsksForFees");
        feeCase.ReportCount.Should().Be(1);

        var queue = await ReadDataAsync<PageShape<CaseShape>>(await HttpClient.GetAsync($"{Trust}/moderation/queue?targetType=job&pageSize=100"));
        queue.Items.Should().Contain(item => item.ItemId == feeCase.ItemId, "a fee demand lists the case on the first report");
        queue.Items.Should().NotContain(item => item.ContentId == otherJob.JobId.ToString(), "one misleading report does not");
    }

    [Fact]
    public async Task A_hide_is_carried_out_on_the_job_with_the_moderators_reason_and_the_case_is_decided_once()
    {
        var job = await PublishedJobAsync("Warehouse Operative");
        await ReportAsync(job.JobId, "Scam", "Asked me to send my bank login.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync(job.JobId);

        await AssignAsync(item.ItemId);

        var decided = await DecideAsync(item.ItemId, "HidePendingReview", "FraudOrScam",
            "Asks applicants for online banking details.");
        decided.StatusCode.Should().Be(HttpStatusCode.OK, await decided.Content.ReadAsStringAsync());

        using (var anonymous = Factory.CreateClient())
        {
            (await anonymous.GetAsync($"{Jobs}/{job.JobId}")).StatusCode
                .Should().Be(HttpStatusCode.NotFound, "the job module carried the hide out");
        }

        var again = await DecideAsync(item.ItemId, "NoAction", "Other", "Changed my mind about this one.");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict, "a case is decided once");

        await Factory.DrainOutboxAsync();

        var reports = await ReadDataAsync<List<ReportShape>>(
            await HttpClient.GetAsync($"{Jobs}/moderation/reports?postingId={job.JobId}&openOnly=false"));
        reports.Should().ContainSingle().Which.Status.Should().Be("Upheld", "the decision closed the module's report");
    }

    [Fact]
    public async Task A_removal_takes_effect_only_when_a_second_member_of_staff_confirms_it()
    {
        var job = await PublishedJobAsync("Delivery Driver");
        await ReportAsync(job.JobId, "FeeRequested", "Wants a fee for the uniform.");
        await Factory.DrainOutboxAsync();

        var (_, first) = await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync(job.JobId);
        await AssignAsync(item.ItemId);

        var proposed = await DecideAsync(item.ItemId, "Remove", "AsksForFees",
            "Charges candidates for a uniform before the first shift.");
        proposed.StatusCode.Should().Be(HttpStatusCode.OK, await proposed.Content.ReadAsStringAsync());

        var pending = await ReadDataAsync<CaseShape>(proposed);
        pending.CurrentOutcome.Should().Be("HidePendingReview", "until it is confirmed, it is only hidden");
        pending.SecondReview.Should().Be("RemovalProposal");

        var selfConfirmed = await SecondReviewAsync(item.ItemId, confirm: true, "I agree with myself, obviously.");
        selfConfirmed.StatusCode.Should().Be(HttpStatusCode.Forbidden, "nobody checks their own decision");

        await SignInAsRoleAsync("Moderator");

        var confirmed = await SecondReviewAsync(item.ItemId, confirm: true, "Checked the advert and the report. Agreed.");
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());

        var removed = await ReadDataAsync<CaseShape>(confirmed);
        removed.CurrentOutcome.Should().Be("Remove");
        removed.AppealDeadline.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(14), TimeSpan.FromMinutes(5));
        removed.Decisions.Select(decision => decision.Kind).Should().Equal("Proposal", "Confirmation");
    }

    [Fact]
    public async Task Staff_can_open_a_case_with_no_report_and_it_waits_for_a_second_check()
    {
        var job = await PublishedJobAsync("Night Security");

        await SignInAsRoleAsync("Moderator");

        var opened = await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/cases",
            new
            {
                contentType = "job",
                contentId = job.JobId,
                reasonCode = "Impersonation",
                note = "Uses the logo and name of a real security firm that says it is not hiring."
            });
        opened.StatusCode.Should().Be(HttpStatusCode.Created, await opened.Content.ReadAsStringAsync());

        var item = await ReadDataAsync<CaseShape>(opened);
        item.StaffInitiated.Should().BeTrue();
        item.SecondReview.Should().Be("StaffInitiated");
        item.SecondReviewDueAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task The_owner_can_ask_for_a_review_and_a_moderator_restores_the_job()
    {
        var job = await PublishedJobAsync("Kitchen Assistant");
        await ReportAsync(job.JobId, "Scam", "The phone number is a premium-rate line.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync(job.JobId);
        await AssignAsync(item.ItemId);
        (await DecideAsync(item.ItemId, "HidePendingReview", "FraudOrScam",
            "Contact number is a premium-rate line.")).EnsureSuccessStatusCode();

        Api.Authenticate(job.OwnerToken);
        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await SignInAsRoleAsync("Moderator");
        await AssignAsync(item.ItemId);

        var restored = await DecideAsync(item.ItemId, "Restore", "Other",
            "The employer replaced the number with their office line.");
        restored.StatusCode.Should().Be(HttpStatusCode.OK, await restored.Content.ReadAsStringAsync());

        using var anonymous = Factory.CreateClient();
        (await anonymous.GetAsync($"{Jobs}/{job.JobId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_member_cannot_ask_for_a_review_of_somebody_elses_case()
    {
        var job = await PublishedJobAsync("Cleaner");
        await ReportAsync(job.JobId, "Scam", "Fake company registration number.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync(job.JobId);

        var (_, stranger) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(stranger.Tokens.AccessToken);

        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private sealed record PublishedJob(Guid JobId, string OwnerToken);

    private async Task<PublishedJob> PublishedJobAsync(string title)
    {
        Factory.EnsureDatabaseCreated();

        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employer = await ReadDataAsync<EmployerShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Jobs}/employers",
                new { displayName = $"Case Loop Ltd {Guid.NewGuid():N}", organizationType = "Healthcare", countryCode = "DE" }));

        var created = await HttpClient.PostAsJsonAsync(
            $"{Jobs}/",
            new
            {
                employerId = employer.EmployerId,
                title,
                description = "A posting created by the integration suite for the moderation case loop.",
                categoryKey = "healthcare",
                jobType = "FullTime",
                experienceLevel = "Mid",
                countryCode = "DE",
                cityName = "Berlin",
                workplaceType = "OnSite"
            });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var job = await ReadDataAsync<JobShape>(created);

        (await HttpClient.PostAsJsonAsync($"{Jobs}/{job.JobId}/publish", new { openImmediately = true }))
            .EnsureSuccessStatusCode();

        return new PublishedJob(job.JobId, owner.Tokens.AccessToken);
    }

    private async Task ReportAsync(Guid jobId, string reason, string detail)
    {
        var (_, reporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporter.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync($"{Jobs}/postings/{jobId}/reports", new { reason, detail });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private async Task<CaseShape> CaseForAsync(Guid jobId) =>
        await ReadDataAsync<CaseShape>(
            await HttpClient.GetAsync($"{Trust}/moderation/cases?contentType=job&contentId={jobId}"));

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
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record PageShape<T>(List<T> Items);

    private sealed record EmployerShape(Guid EmployerId);

    private sealed record JobShape(Guid JobId);

    private sealed record ReportShape(Guid ReportId, string Status);

    private sealed record DecisionShape(string? Kind, string? Outcome);

    private sealed record CaseShape(
        Guid ItemId,
        string ContentId,
        string? ReasonCode,
        int ReportCount,
        string? CurrentOutcome,
        string SecondReview,
        DateTimeOffset? SecondReviewDueAt,
        DateTimeOffset? AppealDeadline,
        bool StaffInitiated,
        List<DecisionShape> Decisions);
}
