using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Infrastructure.Hosting;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Consequences only once a decision stands, the strike ladder, and appeals that undo (7 Nov 2026).
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class ModerationConsequenceTests : PooledIdentityTestBase
{
    private const string Jobs = "/api/v1/jobs";
    private const string Trust = "/api/v1/trust";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ModerationConsequenceTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_removal_costs_nothing_until_its_window_closes_and_then_the_owner_is_warned_and_told_the_points()
    {
        var (job, ownerId, ownerToken, item) = await ConfirmedRemovalAsync("Warehouse Picker");

        (await StrikesAsync(ownerId)).Should().BeEmpty("nothing lands while the owner may still appeal");
        (await RiskEvidenceAsync(ownerId)).Should().BeEmpty(
            "until 7 Nov 2026 the upheld report counted against the owner here, before they could appeal");

        await SweepAsync();
        (await StrikesAsync(ownerId)).Should().BeEmpty("the window is still open");
        (await RiskEvidenceAsync(ownerId)).Should().BeEmpty("the window is still open");

        await CloseWindowAsync(item);
        await SweepAsync();

        // The removal stands, and only now does it count towards the owner's risk.
        (await RiskEvidenceAsync(ownerId)).Should().ContainSingle()
            .Which.Kind.Should().Be(RiskSignalKind.ReportUpheld);

        var strike = (await StrikesAsync(ownerId)).Should().ContainSingle().Subject;
        strike.Level.Should().Be(StrikeLevel.Warning);
        strike.BlockedUntil.Should().BeNull("a warning blocks nothing");

        await Factory.DrainOutboxAsync();
        Api.Authenticate(ownerToken);
        var notices = await InboxAsync();
        var stood = notices.Should().ContainSingle(notice => notice.Title.Contains("means for you")).Subject;
        stood.Body.Should().Contain("This is a warning");
        stood.Body.Should().Contain("went down by 25 points");

        await SweepAsync();
        (await StrikesAsync(ownerId)).Should().ContainSingle("a decision that stands is counted once");
        (await RiskEvidenceAsync(ownerId)).Should().ContainSingle("and counted against the owner once");
    }

    [Fact]
    public async Task An_upheld_appeal_brings_the_job_back_and_neither_decider_may_review_it()
    {
        var (job, _, ownerToken, item) = await ConfirmedRemovalAsync("Line Cook");

        Api.Authenticate(ownerToken);
        var filed = await HttpClient.PostAsJsonAsync(
            $"{Trust}/appeals",
            new
            {
                type = "Content",
                targetId = item.ToString(),
                reason = "The fee in the advert is the cost of the food-hygiene course, which we refund.",
                additionalDocuments = Array.Empty<string>(),
                correctionRequest = false
            });
        filed.StatusCode.Should().Be(HttpStatusCode.Created, await filed.Content.ReadAsStringAsync());
        var appeal = await ReadDataAsync<AppealShape>(filed);

        Api.Authenticate(_proposerToken!);
        (await ReviewAsync(appeal.AppealId, upheld: true)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the proposer decided it");

        Api.Authenticate(_confirmerToken!);
        (await ReviewAsync(appeal.AppealId, upheld: true)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "so did the confirmer");

        await SignInAsRoleAsync("Moderator");
        var reviewed = await ReviewAsync(appeal.AppealId, upheld: true);
        reviewed.StatusCode.Should().Be(HttpStatusCode.OK, await reviewed.Content.ReadAsStringAsync());

        using (var anonymous = Factory.CreateClient())
        {
            (await anonymous.GetAsync($"{Jobs}/{job}")).StatusCode.Should().Be(HttpStatusCode.OK, "the advert is back");
        }

        await Factory.DrainOutboxAsync();
        Api.Authenticate(ownerToken);
        var notices = await InboxAsync();
        notices.Should().Contain(notice => notice.Title.Contains("appeal was upheld"));
        notices.Should().Contain(notice => notice.Title.Contains("is back"));
    }

    [Fact]
    public async Task A_refused_appeal_makes_the_removal_stand_at_once()
    {
        var (_, ownerId, ownerToken, item) = await ConfirmedRemovalAsync("Dish Washer");

        Api.Authenticate(ownerToken);
        var appeal = await ReadDataAsync<AppealShape>(await HttpClient.PostAsJsonAsync(
            $"{Trust}/appeals",
            new
            {
                type = "Content",
                targetId = item.ToString(),
                reason = "I did not mean to ask for money, it was a template.",
                additionalDocuments = Array.Empty<string>(),
                correctionRequest = false
            }));

        await SignInAsRoleAsync("Moderator");
        (await ReviewAsync(appeal.AppealId, upheld: false)).EnsureSuccessStatusCode();

        (await StrikesAsync(ownerId)).Should().ContainSingle("the consequences no longer wait for the window");
    }

    [Fact]
    public async Task A_first_strike_stops_posting_jobs_for_a_week_and_says_until_when()
    {
        Factory.EnsureDatabaseCreated();

        var (_, member) = await RegisterVerifiedMemberAsync();
        var memberId = member.User.Id;

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
            db.Add(Strike.Issue(UserId.From(memberId), ModerationItemId.New(), "jobs", 0, DateTimeOffset.UtcNow.AddDays(-30)));
            db.Add(Strike.Issue(UserId.From(memberId), ModerationItemId.New(), "jobs", 1, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        Api.Authenticate(member.Tokens.AccessToken);

        var employer = await HttpClient.PostAsJsonAsync(
            $"{Jobs}/employers",
            new { displayName = $"Blocked Ltd {Guid.NewGuid():N}", organizationType = "Healthcare", countryCode = "DE" });
        var employerId = (await ReadDataAsync<EmployerShape>(employer)).EmployerId;

        var created = await HttpClient.PostAsJsonAsync($"{Jobs}/", JobBody(employerId, "Night Porter"));

        created.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await created.Content.ReadAsStringAsync();
        body.Should().Contain("You cannot post in jobs until");
        body.Should().Contain(DateTimeOffset.UtcNow.AddDays(7).UtcDateTime.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private string? _proposerToken;
    private string? _confirmerToken;

    /// <summary>A published job, reported, proposed for removal by one moderator and confirmed by another.</summary>
    private async Task<(Guid JobId, Guid OwnerId, string OwnerToken, Guid CaseId)> ConfirmedRemovalAsync(string title)
    {
        Factory.EnsureDatabaseCreated();

        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employerId = (await ReadDataAsync<EmployerShape>(await HttpClient.PostAsJsonAsync(
            $"{Jobs}/employers",
            new { displayName = $"Consequence Ltd {Guid.NewGuid():N}", organizationType = "Healthcare", countryCode = "DE" }))).EmployerId;

        var created = await HttpClient.PostAsJsonAsync($"{Jobs}/", JobBody(employerId, title));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var jobId = (await ReadDataAsync<JobShape>(created)).JobId;
        (await HttpClient.PostAsJsonAsync($"{Jobs}/{jobId}/publish", new { openImmediately = true })).EnsureSuccessStatusCode();

        var (_, reporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporter.Tokens.AccessToken);
        (await HttpClient.PostAsJsonAsync($"{Jobs}/postings/{jobId}/reports",
            new { reason = "FeeRequested", detail = "Asked for 200 euros for a course." })).EnsureSuccessStatusCode();
        await Factory.DrainOutboxAsync();

        var (_, proposer) = await SignInAsRoleAsync("Moderator");
        _proposerToken = proposer.Tokens.AccessToken;
        var caseId = (await ReadDataAsync<CaseShape>(
            await HttpClient.GetAsync($"{Trust}/moderation/cases?contentType=job&contentId={jobId}"))).ItemId;
        (await HttpClient.PostAsync($"{Trust}/moderation/queue/{caseId}/assign", content: null)).EnsureSuccessStatusCode();
        (await HttpClient.PostAsJsonAsync($"{Trust}/moderation/queue/{caseId}/decision",
            new { outcome = "Remove", reasonCode = "AsksForFees", note = "Charges candidates 200 euros before an interview." }))
            .EnsureSuccessStatusCode();

        var (_, confirmer) = await SignInAsRoleAsync("Moderator");
        _confirmerToken = confirmer.Tokens.AccessToken;
        (await HttpClient.PostAsJsonAsync($"{Trust}/moderation/queue/{caseId}/second-review",
            new { confirm = true, note = "Checked the advert and the report. Agreed." })).EnsureSuccessStatusCode();

        return (jobId, owner.User.Id, owner.Tokens.AccessToken, caseId);
    }

    private static object JobBody(Guid employerId, string title) => new
    {
        employerId,
        title,
        description = "A posting created by the integration suite for moderation consequences.",
        categoryKey = "healthcare",
        jobType = "FullTime",
        experienceLevel = "Mid",
        countryCode = "DE",
        cityName = "Berlin",
        workplaceType = "OnSite"
    };

    private async Task CloseWindowAsync(Guid caseId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var item = await db.Set<ModerationItem>().FirstAsync(candidate => candidate.Id == ModerationItemId.From(caseId));
        db.Entry(item).Property(nameof(ModerationItem.AppealDeadline)).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
    }

    private Task<int> SweepAsync() =>
        Factory.Services.GetRequiredService<TrustConsequenceSweep>().RunOnceAsync(CancellationToken.None);

    private async Task<List<RiskSignal>> RiskEvidenceAsync(Guid memberId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var member = UserId.From(memberId);
        return await db.Set<RiskSignal>().AsNoTracking().Where(signal => signal.UserId == member).ToListAsync();
    }

    private async Task<List<Strike>> StrikesAsync(Guid memberId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var member = UserId.From(memberId);
        return await db.Set<Strike>().AsNoTracking().Where(strike => strike.MemberId == member).ToListAsync();
    }

    private Task<HttpResponseMessage> ReviewAsync(Guid appealId, bool upheld) =>
        HttpClient.PostAsJsonAsync(
            $"{Trust}/appeals/{appealId}/review",
            new
            {
                decision = upheld ? "Approve" : "Reject",
                explanation = upheld
                    ? "The fee is a refundable course cost, stated clearly. Restored."
                    : "The advert charges candidates before any interview. The decision stands."
            });

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
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("title", out var title) && element.TryGetProperty("body", out var body))
            {
                into.Add(new NoticeShape(title.GetString() ?? string.Empty, body.GetString() ?? string.Empty));
                return;
            }

            foreach (var property in element.EnumerateObject())
            {
                Collect(property.Value, into);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Collect(item, into);
            }
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

    private sealed record AppealShape(Guid AppealId);

    private sealed record NoticeShape(string Title, string Body);
}
