using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Staff act on a member's job only through the moderation routes.
/// <para>
/// <b>Until 7 Nov 2026 every employer check began with "if the caller is staff, allow".</b> A
/// moderator could edit, publish, close or archive anybody's posting, rewrite the employer's legal
/// name and contacts, and shortlist, reject, interview or make offers to its applicants in the
/// employer's name, with no report, no reason and no word to the employer. The owner's routes now
/// refuse staff like any outsider, and staff act through <c>/jobs/moderation/*</c>.
/// </para>
/// <para>
/// Each refusal is asserted by its error code as well as its status, so a test cannot pass
/// because the endpoint's policy turned staff away before the owner check was ever asked.
/// </para>
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class JobsStaffBoundaryTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/jobs";

    private const string NotEmployer = "jobs.not-employer";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JobsStaffBoundaryTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>An edit and a state change on the owner's routes: staff refused, the owner not.</summary>
    [Fact]
    public async Task Staff_cannot_edit_or_close_a_members_job_and_the_employer_still_can()
    {
        var job = await PublishedJobAsync("Care Assistant");

        await SignInAsRoleAsync("Moderator");

        var edited = await HttpClient.PutAsJsonAsync($"{Root}/{job.JobId}", EditBody("Edited by staff"));
        edited.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await edited.Content.ReadAsStringAsync()).Should().Contain(NotEmployer);

        var closed = await HttpClient.PostAsJsonAsync(
            $"{Root}/{job.JobId}/close", new { outcome = "withdrawn", reason = "Staff closed it." });
        closed.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await closed.Content.ReadAsStringAsync()).Should().Contain(NotEmployer);

        var asStaff = await ReadJsonAsync(await HttpClient.GetAsync($"{Root}/{job.JobId}"));
        asStaff.GetProperty("title").GetString().Should().Be("Care Assistant", "staff changed nothing");
        asStaff.GetProperty("status").GetString().Should().NotBe("Closed");

        // The employer, on the same two routes.
        Api.Authenticate(job.OwnerToken);

        var ownerEdited = await HttpClient.PutAsJsonAsync(
            $"{Root}/{job.JobId}", EditBody("Senior Care Assistant"));
        ownerEdited.StatusCode.Should().Be(
            HttpStatusCode.OK, await ownerEdited.Content.ReadAsStringAsync());

        var ownerClosed = await HttpClient.PostAsJsonAsync(
            $"{Root}/{job.JobId}/close", new { outcome = "withdrawn" });
        ownerClosed.StatusCode.Should().Be(
            HttpStatusCode.OK, await ownerClosed.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// The employer's own profile — its legal name, registration number and contacts — is the
    /// employer's to change. An administrator is refused like a moderator.
    /// </summary>
    [Fact]
    public async Task An_admin_cannot_rewrite_a_members_employer_profile_and_the_employer_still_can()
    {
        var job = await PublishedJobAsync("Night Porter");

        await SignInAsRoleAsync("Admin");

        var rewritten = await HttpClient.PutAsJsonAsync(
            $"{Root}/employers/{job.EmployerId}", EmployerBody("Somebody Else Ltd"));
        rewritten.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await rewritten.Content.ReadAsStringAsync()).Should().Contain(NotEmployer);

        Api.Authenticate(job.OwnerToken);

        var ownerUpdated = await HttpClient.PutAsJsonAsync(
            $"{Root}/employers/{job.EmployerId}", EmployerBody("Harbour Hotels Ltd"));
        ownerUpdated.StatusCode.Should().Be(
            HttpStatusCode.OK, await ownerUpdated.Content.ReadAsStringAsync());
    }

    /// <summary>Hiring in the employer's name: staff cannot move an applicant; the employer can.</summary>
    [Fact]
    public async Task Staff_cannot_decide_a_members_applicant_and_the_employer_still_can()
    {
        var job = await PublishedJobAsync("Warehouse Operative");

        var (_, candidate) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(candidate.Tokens.AccessToken);

        var applied = await HttpClient.PostAsJsonAsync(
            $"{Root}/{job.JobId}/apply", new { coverLetter = (string?)null });
        applied.StatusCode.Should().Be(
            HttpStatusCode.Created, await applied.Content.ReadAsStringAsync());

        var application = await ReadDataAsync<ApplicationShape>(applied);

        await SignInAsRoleAsync("Moderator");

        var decided = await HttpClient.PostAsJsonAsync(
            $"{Root}/applications/{application.ApplicationId}/decide",
            new { status = "Rejected", reason = "Rejected by staff." });
        decided.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await decided.Content.ReadAsStringAsync()).Should().Contain(NotEmployer);

        // The applicants are the employer's to see. Moderating the advert never needs their names.
        var applicants = await HttpClient.GetAsync($"{Root}/applications?employerId={job.EmployerId}");
        applicants.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await applicants.Content.ReadAsStringAsync()).Should().Contain(NotEmployer);

        Api.Authenticate(job.OwnerToken);

        (await HttpClient.GetAsync($"{Root}/applications?employerId={job.EmployerId}")).StatusCode
            .Should().Be(HttpStatusCode.OK, "the employer still reads its own applicants");

        var ownerDecided = await HttpClient.PostAsJsonAsync(
            $"{Root}/applications/{application.ApplicationId}/decide",
            new { status = "UnderReview" });
        ownerDecided.StatusCode.Should().Be(
            HttpStatusCode.OK, await ownerDecided.Content.ReadAsStringAsync());

        (await ReadDataAsync<ApplicationShape>(ownerDecided)).Status.Should().Be("UnderReview");
    }

    /// <summary>
    /// What replaces the shortcut still works: a moderator hides the advert through the moderation
    /// route, the public can no longer open it, and staff can still read it to review it.
    /// </summary>
    [Fact]
    public async Task A_moderator_still_hides_a_members_job_through_the_moderation_route()
    {
        var job = await PublishedJobAsync("Delivery Driver");

        await SignInAsRoleAsync("Moderator");

        var hidden = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/postings/{job.JobId}",
            new { action = "Hide", reason = "Asks candidates for a registration fee." });
        hidden.StatusCode.Should().Be(HttpStatusCode.OK, await hidden.Content.ReadAsStringAsync());

        using var anonymous = Factory.CreateClient();
        (await anonymous.GetAsync($"{Root}/{job.JobId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "a hidden advert is not public");

        (await HttpClient.GetAsync($"{Root}/{job.JobId}")).StatusCode
            .Should().Be(HttpStatusCode.OK, "staff still read hidden adverts to review them");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private sealed record PublishedJob(Guid JobId, Guid EmployerId, string OwnerToken);

    /// <summary>A published advert by a fresh employer. Leaves the caller signed in as it.</summary>
    private async Task<PublishedJob> PublishedJobAsync(string title)
    {
        Factory.EnsureDatabaseCreated();

        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employer = await ReadDataAsync<EmployerShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/employers",
                new
                {
                    displayName = $"Harbour Hotels {Guid.NewGuid():N}",
                    organizationType = "Healthcare",
                    countryCode = "DE"
                }));

        var created = await HttpClient.PostAsJsonAsync(
            $"{Root}/",
            new
            {
                employerId = employer.EmployerId,
                title,
                description = "A posting created by the integration suite for staff boundary tests.",
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
            $"{Root}/{job.JobId}/publish", new { openImmediately = true });
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        return new PublishedJob(job.JobId, employer.EmployerId, owner.Tokens.AccessToken);
    }

    private static object EditBody(string title) => new
    {
        title,
        description = "A posting created by the integration suite for staff boundary tests.",
        jobType = "FullTime",
        experienceLevel = "Mid",
        countryCode = "DE"
    };

    private static object EmployerBody(string legalName) => new
    {
        displayName = "Harbour Hotels",
        legalName,
        registrationNumber = "HRB 123456",
        organizationType = "Healthcare",
        countryCode = "DE",
        contactEmail = "jobs@harbour-hotels.example"
    };

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("data").Clone();
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

    private sealed record ApplicationShape(Guid ApplicationId, string Status);
}
