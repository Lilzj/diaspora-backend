using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The owner is told about every staff decision on their content, with the reason and the note, and
/// reporters hear the outcome without detail (7 Nov 2026).
/// <para>
/// Read back from the inbox, as rendered text: a notice that exists in a contributor but whose
/// template drops a value, or whose rule never matches, reaches nobody, and only the rendered body
/// shows that.
/// </para>
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class ModerationNoticeTests : PooledIdentityTestBase
{
    private const string Jobs = "/api/v1/jobs";
    private const string Trust = "/api/v1/trust";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ModerationNoticeTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task The_owner_reads_why_their_job_was_hidden_and_the_reporter_hears_action_was_taken()
    {
        var job = await PublishedJobAsync("Hotel Porter, visa included");
        var reporterToken = await ReportAsync(job.JobId, "FeeRequested", "They want 300 euros for paperwork.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync(job.JobId);
        await AssignAsync(item);
        (await DecideAsync(item, "HidePendingReview", "AsksForFees",
                "The advert asks candidates to pay 300 euros for visa paperwork."))
            .EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();

        Api.Authenticate(job.OwnerToken);
        var ownerNotices = await InboxAsync();
        var hidden = ownerNotices.Should().ContainSingle(notice => notice.Title.Contains("hidden while Diaspora reviews it")).Subject;
        hidden.Body.Should().Contain("Hotel Porter, visa included");
        hidden.Body.Should().Contain("asking people to pay up front", "the reason is in the notice");
        hidden.Body.Should().Contain("The advert asks candidates to pay 300 euros for visa paperwork.", "so is the moderator's note");
        hidden.Body.Should().Contain("ask for a review", "and what the owner can do next");
        hidden.TargetRoute.Should().Be($"/profile/moderation/{item}");

        Api.Authenticate(reporterToken);
        var reporterNotices = await InboxAsync();
        var outcome = reporterNotices.Should().ContainSingle(notice => notice.Title.Contains("you reported")).Subject;
        outcome.Body.Should().Contain("took action");
        outcome.Body.Should().NotContain("300 euros", "a reporter is told the outcome, not the decision");
    }

    [Fact]
    public async Task A_confirmed_removal_tells_the_owner_until_when_they_can_appeal()
    {
        var job = await PublishedJobAsync("Factory Packer");
        await ReportAsync(job.JobId, "Scam", "The company does not exist.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync(job.JobId);
        await AssignAsync(item);
        (await DecideAsync(item, "Remove", "FraudOrScam", "No such company at the registered address."))
            .EnsureSuccessStatusCode();

        await SignInAsRoleAsync("Moderator");
        (await HttpClient.PostAsJsonAsync(
                $"{Trust}/moderation/queue/{item}/second-review",
                new { confirm = true, note = "Checked the register myself. Agreed." }))
            .EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();

        Api.Authenticate(job.OwnerToken);
        var notices = await InboxAsync();

        var removed = notices.Should().ContainSingle(notice => notice.Title.Contains("was removed")).Subject;
        removed.Body.Should().Contain("No such company at the registered address.", "the proposer's note is what the owner reads");
        removed.Body.Should().Contain("Two members of staff agreed");
        removed.Body.Should().Contain(DateTimeOffset.UtcNow.AddDays(14).UtcDateTime.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task The_owner_can_read_their_own_decision_and_nobody_else_can()
    {
        var job = await PublishedJobAsync("Gardener");
        await ReportAsync(job.JobId, "Scam", "Asks for a deposit.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync(job.JobId);
        await AssignAsync(item);
        (await DecideAsync(item, "HidePendingReview", "FraudOrScam", "Asks applicants for a deposit."))
            .EnsureSuccessStatusCode();

        Api.Authenticate(job.OwnerToken);
        var mine = await ReadDataAsync<MyCaseShape>(await HttpClient.GetAsync($"{Trust}/moderation/cases/{item}/mine"));
        mine.Outcome.Should().Be("HidePendingReview");
        mine.ReasonLabel.Should().Be("a fraud or scam");
        mine.Note.Should().Be("Asks applicants for a deposit.");
        mine.CanRequestReview.Should().BeTrue();

        var (_, stranger) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(stranger.Tokens.AccessToken);
        (await HttpClient.GetAsync($"{Trust}/moderation/cases/{item}/mine")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
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
                new { displayName = $"Notice Ltd {Guid.NewGuid():N}", organizationType = "Healthcare", countryCode = "DE" }));

        var created = await HttpClient.PostAsJsonAsync(
            $"{Jobs}/",
            new
            {
                employerId = employer.EmployerId,
                title,
                description = "A posting created by the integration suite for moderation notices.",
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

    private async Task<string> ReportAsync(Guid jobId, string reason, string detail)
    {
        var (_, reporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporter.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync($"{Jobs}/postings/{jobId}/reports", new { reason, detail });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return reporter.Tokens.AccessToken;
    }

    private async Task<Guid> CaseForAsync(Guid jobId) =>
        (await ReadDataAsync<CaseShape>(
            await HttpClient.GetAsync($"{Trust}/moderation/cases?contentType=job&contentId={jobId}"))).ItemId;

    private async Task AssignAsync(Guid itemId) =>
        (await HttpClient.PostAsync($"{Trust}/moderation/queue/{itemId}/assign", content: null))
            .EnsureSuccessStatusCode();

    private Task<HttpResponseMessage> DecideAsync(Guid itemId, string outcome, string reasonCode, string note) =>
        HttpClient.PostAsJsonAsync($"{Trust}/moderation/queue/{itemId}/decision", new { outcome, reasonCode, note });

    /// <summary>The caller's moderation notices, as rendered.</summary>
    private async Task<List<NoticeShape>> InboxAsync()
    {
        var response = await HttpClient.GetAsync("/api/v1/notifications?category=moderation&limit=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var notices = new List<NoticeShape>();
        Collect(document.RootElement, notices);
        return notices;
    }

    private static void Collect(JsonElement element, List<NoticeShape> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("title", out var title) && element.TryGetProperty("body", out var body))
                {
                    into.Add(new NoticeShape(
                        title.GetString() ?? string.Empty,
                        body.GetString() ?? string.Empty,
                        element.TryGetProperty("targetRoute", out var route) ? route.GetString() : null));
                    return;
                }

                foreach (var property in element.EnumerateObject())
                {
                    Collect(property.Value, into);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, into);
                }

                break;
        }
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record EmployerShape(Guid EmployerId);

    private sealed record JobShape(Guid JobId);

    private sealed record CaseShape(Guid ItemId);

    private sealed record NoticeShape(string Title, string Body, string? TargetRoute);

    private sealed record MyCaseShape(string? Outcome, string? ReasonLabel, string? Note, bool CanRequestReview);
}
