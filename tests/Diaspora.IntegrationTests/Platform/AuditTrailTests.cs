using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// The audit trail is stored and can be read back (7 Nov 2026).
/// <para>
/// Before this, <c>[Auditable]</c> wrote a log line with an empty entity id and no reason, and wrote
/// it whether or not the action succeeded. The owner's rule for staff power needs a record that says
/// who acted, on what, and why, and that a refused action does not appear in.
/// </para>
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class AuditTrailTests : PooledIdentityTestBase
{
    private const string Jobs = "/api/v1/jobs";
    private const string Audit = "/api/v1/platform/audit";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AuditTrailTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_moderators_hide_is_stored_with_who_acted_on_what_and_why()
    {
        var job = await PublishedJobAsync("Night Porter");

        var (_, reporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporter.Tokens.AccessToken);
        (await HttpClient.PostAsJsonAsync(
                $"{Jobs}/postings/{job.JobId}/reports",
                new { reason = "FeeRequested", detail = "Asked for a registration fee." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        await Factory.DrainOutboxAsync();

        var (moderatorId, _) = await SignInAsRoleAsync("Moderator");

        var item = await ReadDataAsync<CaseShape>(
            await HttpClient.GetAsync($"/api/v1/trust/moderation/cases?contentType=job&contentId={job.JobId}"));

        (await HttpClient.PostAsync($"/api/v1/trust/moderation/queue/{item.ItemId}/assign", content: null))
            .EnsureSuccessStatusCode();

        // Since stage 5 a hide is a decision on the Trust case, and it is recorded against the case.
        var hidden = await HttpClient.PostAsJsonAsync(
            $"/api/v1/trust/moderation/queue/{item.ItemId}/decision",
            new
            {
                outcome = "HidePendingReview",
                reasonCode = "AsksForFees",
                note = "Asks candidates for a registration fee."
            });
        hidden.StatusCode.Should().Be(HttpStatusCode.OK, await hidden.Content.ReadAsStringAsync());

        await SignInAsRoleAsync("Admin");

        var trail = await ReadTrailAsync($"{Audit}?entityId={item.ItemId}");

        var entry = trail.Should().ContainSingle(record => record.Action == "trust.case.decided").Subject;
        entry.EntityId.Should().Be(item.ItemId.ToString());
        entry.ActorUserId.Should().Be(moderatorId.ToString());
        entry.ActorRole.Should().Be("Moderator");
        entry.Reason.Should().Be("Asks candidates for a registration fee.");
    }

    [Fact]
    public async Task A_refused_action_leaves_no_entry()
    {
        var job = await PublishedJobAsync("Kitchen Porter");

        await SignInAsRoleAsync("Moderator");

        // Since stage 2 the owner's route refuses staff. The refusal changed nothing, so it is not
        // a thing that happened, and the trail does not say it did.
        var edited = await HttpClient.PutAsJsonAsync(
            $"{Jobs}/{job.JobId}",
            new
            {
                title = "Edited by staff",
                description = "A posting created by the integration suite for audit trail tests.",
                categoryKey = "healthcare",
                jobType = "FullTime",
                experienceLevel = "Mid",
                countryCode = "DE",
                cityName = "Berlin",
                workplaceType = "OnSite"
            });
        edited.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await SignInAsRoleAsync("Admin");

        var trail = await ReadTrailAsync($"{Audit}?entityId={job.JobId}");

        trail.Should().NotContain(record => record.EntityType == "UpdateJobCommand");
    }

    [Fact]
    public async Task Staff_reading_a_members_security_history_is_recorded()
    {
        Factory.EnsureDatabaseCreated();

        var (_, member) = await RegisterVerifiedMemberAsync();
        var memberId = member.User.Id;

        var (adminId, _) = await SignInAsRoleAsync("Admin");

        (await HttpClient.GetAsync($"/api/v1/admin/accounts/{memberId}/security-events")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var trail = await ReadTrailAsync($"{Audit}?entityId={memberId}&action=identity.member.");

        var entry = trail.Should().ContainSingle(record => record.Action == "identity.member.security-events.read").Subject;
        entry.ActorUserId.Should().Be(adminId.ToString());
    }

    [Fact]
    public async Task Moderators_cannot_read_the_trail_that_records_their_own_decisions()
    {
        Factory.EnsureDatabaseCreated();

        await SignInAsRoleAsync("Moderator");

        (await HttpClient.GetAsync(Audit)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private sealed record PublishedJob(Guid JobId);

    private async Task<PublishedJob> PublishedJobAsync(string title)
    {
        Factory.EnsureDatabaseCreated();

        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employer = await ReadDataAsync<EmployerShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Jobs}/employers",
                new
                {
                    displayName = $"Audit Hotels {Guid.NewGuid():N}",
                    organizationType = "Healthcare",
                    countryCode = "DE"
                }));

        var created = await HttpClient.PostAsJsonAsync(
            $"{Jobs}/",
            new
            {
                employerId = employer.EmployerId,
                title,
                description = "A posting created by the integration suite for audit trail tests.",
                categoryKey = "healthcare",
                jobType = "FullTime",
                experienceLevel = "Mid",
                countryCode = "DE",
                cityName = "Berlin",
                workplaceType = "OnSite"
            });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var job = await ReadDataAsync<JobShape>(created);

        var published = await HttpClient.PostAsJsonAsync(
            $"{Jobs}/{job.JobId}/publish", new { openImmediately = true });
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        return new PublishedJob(job.JobId);
    }

    private async Task<List<AuditShape>> ReadTrailAsync(string url)
    {
        var response = await HttpClient.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await ReadDataAsync<List<AuditShape>>(response);
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

    private sealed record AuditShape(
        Guid Id,
        string? ActorUserId,
        string? ActorRole,
        string Action,
        string EntityType,
        string? EntityId,
        string? Reason);
}
