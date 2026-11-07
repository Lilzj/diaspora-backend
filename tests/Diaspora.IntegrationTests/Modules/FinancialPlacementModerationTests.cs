using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Paid placements over content a moderation decision hid, end to end on a job advert (7 Nov 2026).
/// <para>
/// A hide pauses the advert's running placement, banking its days; a restore resumes it with exactly
/// the days it had left; the buyer cannot resume it while the advert is hidden; and a placement the
/// buyer paused themselves is never resumed by a moderator. Nothing is cancelled or refunded.
/// </para>
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class FinancialPlacementModerationTests : PooledIdentityTestBase
{
    private const string Jobs = "/api/v1/jobs";
    private const string Trust = "/api/v1/trust";
    private const string Financial = "/api/v1/financial";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FinancialPlacementModerationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_hidden_jobs_placement_pauses_and_a_restore_resumes_it_with_its_remaining_time()
    {
        var job = await PublishedJobAsync("Warehouse Supervisor");
        var placementId = await RunningPlacementOverAsync(job.JobId, job.OwnerId);
        var before = await PlacementAsync(placementId);

        await SignInAsRoleAsync("Moderator");
        var item = await OpenCaseAsync(job.JobId, "Asks applicants to pay for their own background check.");
        await AssignAsync(item.ItemId);
        (await DecideAsync(item.ItemId, "HidePendingReview", "AsksForFees",
            "Asks applicants to pay for their own background check.")).EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();

        var paused = await PlacementAsync(placementId);
        paused.Status.Should().Be(PlacementStatus.Paused, "a hidden advert's promotion stops");
        paused.ModerationCaseId.Should().Be(item.ItemId);
        paused.Window.EndsAt.Should().Be(before.Window.EndsAt, "pausing banks the days; it does not spend them");

        // The buyer cannot switch it back on while the advert is hidden.
        Api.Authenticate(job.OwnerToken);
        (await HttpClient.PostAsync($"{Financial}/placements/{placementId}/resume", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await SignInAsRoleAsync("Moderator");
        await AssignAsync(item.ItemId);
        (await DecideAsync(item.ItemId, "Restore", "Other",
            "The employer removed the fee and pays for the check itself.")).EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();

        var resumed = await PlacementAsync(placementId);
        resumed.Status.Should().Be(PlacementStatus.Active);
        resumed.ModerationCaseId.Should().BeNull();
        resumed.PausedDuration.Should().BePositive();

        // Within the test database's 0.1 ms storage precision: the pause itself lasted far longer.
        resumed.Window.EndsAt.Should().BeCloseTo(
            before.Window.EndsAt!.Value + resumed.PausedDuration,
            TimeSpan.FromMilliseconds(1),
            "the buyer gets back exactly the time the hide took");
    }

    [Fact]
    public async Task A_placement_the_buyer_paused_is_not_resumed_by_a_restore()
    {
        var job = await PublishedJobAsync("Forklift Driver");
        var placementId = await RunningPlacementOverAsync(job.JobId, job.OwnerId);

        Api.Authenticate(job.OwnerToken);
        var buyerPause = await HttpClient.PostAsync($"{Financial}/placements/{placementId}/pause", content: null);
        buyerPause.StatusCode.Should().Be(HttpStatusCode.OK, await buyerPause.Content.ReadAsStringAsync());

        var pausedByBuyer = await PlacementAsync(placementId);

        await SignInAsRoleAsync("Moderator");
        var item = await OpenCaseAsync(job.JobId, "The company name belongs to a firm that is not hiring.");
        await AssignAsync(item.ItemId);
        (await DecideAsync(item.ItemId, "HidePendingReview", "Impersonation",
            "The company name belongs to a firm that is not hiring.")).EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();

        (await PlacementAsync(placementId)).ModerationCaseId
            .Should().BeNull("the buyer had already paused it; the decision did not");

        Api.Authenticate(job.OwnerToken);
        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await SignInAsRoleAsync("Moderator");
        await AssignAsync(item.ItemId);
        (await DecideAsync(item.ItemId, "Restore", "Other",
            "The employer showed the firm's letter of authority.")).EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();

        var after = await PlacementAsync(placementId);
        after.Status.Should().Be(PlacementStatus.Paused, "the buyer paused it, and only the buyer resumes it");
        after.PausedAt.Should().Be(pausedByBuyer.PausedAt);
        after.Window.EndsAt.Should().Be(pausedByBuyer.Window.EndsAt);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private sealed record PublishedJob(Guid JobId, Guid OwnerId, string OwnerToken);

    private async Task<PublishedJob> PublishedJobAsync(string title)
    {
        Factory.EnsureDatabaseCreated();

        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employer = await ReadDataAsync<EmployerShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Jobs}/employers",
                new { displayName = $"Placement Case Ltd {Guid.NewGuid():N}", organizationType = "Healthcare", countryCode = "DE" }));

        var created = await HttpClient.PostAsJsonAsync(
            $"{Jobs}/",
            new
            {
                employerId = employer.EmployerId,
                title,
                description = "A posting created by the integration suite for paid placements under moderation.",
                categoryKey = "healthcare",
                jobType = "FullTime",
                experienceLevel = "Mid",
                countryCode = "DE",
                cityName = "Hamburg",
                workplaceType = "OnSite"
            });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var job = await ReadDataAsync<JobShape>(created);

        (await HttpClient.PostAsJsonAsync($"{Jobs}/{job.JobId}/publish", new { openImmediately = true }))
            .EnsureSuccessStatusCode();

        return new PublishedJob(job.JobId, owner.User.Id, owner.Tokens.AccessToken);
    }

    /// <summary>
    /// A seven-day boost the employer bought over the advert, in force. Written straight to the
    /// repository: the purchase path is not what this file is about.
    /// </summary>
    private async Task<Guid> RunningPlacementOverAsync(Guid jobId, Guid buyerId)
    {
        using var scope = Factory.Services.CreateScope();

        var placements = scope.ServiceProvider.GetRequiredService<IPlacementRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var now = DateTimeOffset.UtcNow;

        var placement = Placement.Purchase(
            PlacementId.New(),
            SubjectReference.Create(MonetizationSubjectType.Job, jobId),
            PlacementKind.Boost,
            DateWindow.ForDays(now, 7),
            1.5m,
            buyerId,
            "job.boost.7d",
            now);

        placement.Activate(now);
        placements.Add(placement);

        await unitOfWork.SaveChangesAsync();

        return placement.Id.Value;
    }

    private async Task<Placement> PlacementAsync(Guid placementId)
    {
        using var scope = Factory.Services.CreateScope();

        var placement = await scope.ServiceProvider.GetRequiredService<IPlacementRepository>()
            .FindAsync(PlacementId.From(placementId));

        placement.Should().NotBeNull();

        return placement!;
    }

    private async Task<CaseShape> OpenCaseAsync(Guid jobId, string note)
    {
        var opened = await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/cases",
            new { contentType = "job", contentId = jobId, reasonCode = "FraudOrScam", note });

        opened.StatusCode.Should().Be(HttpStatusCode.Created, await opened.Content.ReadAsStringAsync());

        return await ReadDataAsync<CaseShape>(opened);
    }

    private async Task AssignAsync(Guid itemId) =>
        (await HttpClient.PostAsync($"{Trust}/moderation/queue/{itemId}/assign", content: null))
            .EnsureSuccessStatusCode();

    private Task<HttpResponseMessage> DecideAsync(Guid itemId, string outcome, string reasonCode, string note) =>
        HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{itemId}/decision", new { outcome, reasonCode, note });

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
}
