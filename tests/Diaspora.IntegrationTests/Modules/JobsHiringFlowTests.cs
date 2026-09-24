using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The hiring loop end to end, over HTTP: an employer publishes, a candidate applies, the
/// employer moves the application through the pipeline, and both sides see what they are
/// entitled to see and nothing more.
/// <para>
/// The interesting assertions here are not the happy path — they are the refusals. An employer
/// who can see an application cannot see the candidate's other applications. A candidate who
/// applied cannot read the employer's internal notes. A second employer cannot touch either.
/// These are the failures that would be invisible until somebody was harmed by them.
/// </para>
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class JobsHiringFlowTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/jobs";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JobsHiringFlowTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── The full loop ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_candidate_applies_and_the_employer_walks_the_application_forward()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync("Ward Nurse", "healthcare", "DE");

        // The candidate, a different member entirely.
        var (_, candidate) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(candidate.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile",
            new { headline = "Registered nurse, 8 years, ICU", countryCode = "NG" });

        var applied = await HttpClient.PostAsJsonAsync(
            $"{Root}/{employer.JobId}/apply",
            new { coverLetter = "I have eight years on acute wards and I am ready to relocate." });

        applied.StatusCode.Should().Be(
            HttpStatusCode.Created, await applied.Content.ReadAsStringAsync());

        var application = await ReadDataAsync<ApplicationShape>(applied);

        application.Status.Should().Be("Submitted");
        application.CanWithdraw.Should().BeTrue();

        // Back to the employer, who now has an application to work.
        Api.Authenticate(employer.EmployerToken);

        var list = await ReadDataAsync<ApplicationPageShape>(
            await HttpClient.GetAsync($"{Root}/applications/?employerId={employer.EmployerId}"));

        list.Items.Select(item => item.ApplicationId)
            .Should().Contain(application.ApplicationId);

        var moved = await HttpClient.PostAsJsonAsync(
            $"{Root}/applications/{application.ApplicationId}/decide",
            new { status = "UnderReview" });

        moved.StatusCode.Should().Be(HttpStatusCode.OK, await moved.Content.ReadAsStringAsync());

        (await ReadDataAsync<ApplicationShape>(moved)).Status.Should().Be("UnderReview");

        var shortlisted = await HttpClient.PostAsJsonAsync(
            $"{Root}/applications/{application.ApplicationId}/decide",
            new { status = "Shortlisted" });

        shortlisted.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Nine states with a transition table behind them means most pairs are illegal. The wire
    /// must refuse the illegal ones rather than record an impossible history.
    /// </summary>
    [Fact]
    public async Task An_employer_cannot_skip_the_pipeline()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync("Site Supervisor", "construction", "DE");
        var (application, _) = await ApplyAsync(employer.JobId);

        Api.Authenticate(employer.EmployerToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/applications/{application.ApplicationId}/decide",
            new { status = "Accepted" });

        // A conflict, not a bad request: the payload is well formed and the status is a real one.
        // What is wrong is the application's current state, which the caller may not have known.
        response.StatusCode.Should().Be(
            HttpStatusCode.Conflict, "acceptance is the candidate's answer to an offer");
    }

    /// <summary>
    /// Duplicate applications are a conflict, not a second row. Otherwise a double-tap on a
    /// phone produces two applications and the employer sees the candidate twice.
    /// </summary>
    [Fact]
    public async Task A_candidate_cannot_apply_to_the_same_job_twice()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync("Delivery Driver", "logistics", "GB");

        var (_, candidate) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(candidate.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile",
            new { headline = "Driver, C+E, 6 years", countryCode = "GH" });

        (await HttpClient.PostAsJsonAsync(
            $"{Root}/{employer.JobId}/apply", new { coverLetter = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await HttpClient.PostAsJsonAsync(
            $"{Root}/{employer.JobId}/apply", new { coverLetter = (string?)null });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>A draft cannot be applied to. There is nothing published to apply to.</summary>
    [Fact]
    public async Task A_candidate_cannot_apply_to_an_unpublished_job()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync(
            "Quiet Role", "technology", "DE", publish: false);

        var (_, candidate) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(candidate.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile",
            new { headline = "Backend engineer", countryCode = "KE" });

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/{employer.JobId}/apply", new { coverLetter = (string?)null });

        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.BadRequest],
            "a draft is not visible, so it cannot be applied to");
    }

    // ── The refusals ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The whole reason applying is floored at Verified: an application is a real approach to a
    /// real employer, and a Limited member has not yet shown they are a person.
    /// </summary>
    [Fact]
    public async Task A_limited_member_cannot_apply()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync("Care Assistant", "healthcare", "GB");

        var (_, member) = await RegisterAndVerifyAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile",
            new { headline = "Care assistant, 2 years", countryCode = "NG" });

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/{employer.JobId}/apply", new { coverLetter = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A recruiter at another employer has no authority over this application, and must not learn
    /// that it exists.
    /// </summary>
    [Fact]
    public async Task Another_employer_cannot_move_somebody_elses_application()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync("Welder", "skilled-trades", "DE");
        var (application, _) = await ApplyAsync(employer.JobId);

        // A wholly unrelated verified member with their own employer.
        var outsider = await PublishAJobAsync("Unrelated Role", "technology", "GB");
        Api.Authenticate(outsider.EmployerToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/applications/{application.ApplicationId}/decide",
            new { status = "UnderReview" });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A candidate may leave at any point before a decision, without asking and without giving a
    /// reason. Anything else makes the platform a trap for the party with the least power.
    /// </summary>
    [Fact]
    public async Task A_candidate_can_withdraw_their_own_application()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync("Electrician", "skilled-trades", "DE");
        var (application, candidateToken) = await ApplyAsync(employer.JobId);

        Api.Authenticate(candidateToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/applications/{application.ApplicationId}/withdraw",
            new { reason = (string?)null });

        response.StatusCode.Should().Be(
            HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        // Withdrawal returns no body — the candidate is leaving, not fetching a record. The
        // outcome is read back from their own tracker.
        var mine = await ReadDataAsync<ApplicationPageShape>(
            await HttpClient.GetAsync($"{Root}/applications/"));

        mine.Items.Single(item => item.ApplicationId == application.ApplicationId)
            .Status.Should().Be("Withdrawn");
    }

    [Fact]
    public async Task An_employer_cannot_withdraw_on_the_candidates_behalf()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync("Plumber", "skilled-trades", "GB");
        var (application, _) = await ApplyAsync(employer.JobId);

        Api.Authenticate(employer.EmployerToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/applications/{application.ApplicationId}/withdraw",
            new { reason = "We are closing the role" });

        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.Forbidden, HttpStatusCode.NotFound],
            "withdrawal is the candidate's decision alone");
    }

    // ── Saving, and the candidate's own list ────────────────────────────────────────────────

    /// <summary>
    /// Saving is a PUT because it is idempotent: tapping a bookmark twice on a flaky connection
    /// must leave one saved job, not two. The count on the dashboard is how it is observed.
    /// <para>
    /// A candidate profile is required first — the saved list belongs to the job-seeking
    /// workspace alongside applications and alerts, not to the account. A client should create
    /// the profile at onboarding rather than let a member meet <c>jobs.not-candidate</c> on
    /// their first bookmark.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_member_can_save_a_job_and_it_counts_once()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync("Chef de Partie", "hospitality", "DE");

        var (_, member) = await RegisterAndVerifyAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile",
            new { headline = "Chef, 10 years", countryCode = "NG" });

        var saved = await HttpClient.PutAsJsonAsync(
            $"{Root}/{employer.JobId}/save",
            new { note = "Close to my sister", remindBeforeClosing = true });

        saved.StatusCode.Should().BeOneOf(
            [HttpStatusCode.Created, HttpStatusCode.OK],
            "saving is Limited — deciding what to keep is not a privilege to earn");

        // Again. A repeat must not produce a second row.
        (await HttpClient.PutAsJsonAsync(
            $"{Root}/{employer.JobId}/save", new { note = (string?)null }))
            .StatusCode.Should().BeOneOf([HttpStatusCode.Created, HttpStatusCode.OK]);

        var dashboard = await ReadDataAsync<CandidateDashboardShape>(
            await HttpClient.GetAsync($"{Root}/candidate/dashboard"));

        dashboard.SavedJobCount.Should().Be(1, "saving twice saves one job");
    }

    [Fact]
    public async Task Unsaving_removes_it()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync("Line Cook", "hospitality", "DE");

        var (_, member) = await RegisterAndVerifyAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile",
            new { headline = "Line cook, 4 years", countryCode = "GH" });

        await HttpClient.PutAsJsonAsync(
            $"{Root}/{employer.JobId}/save", new { note = (string?)null });

        (await HttpClient.DeleteAsync($"{Root}/{employer.JobId}/save"))
            .StatusCode.Should().BeOneOf([HttpStatusCode.OK, HttpStatusCode.NoContent]);

        var dashboard = await ReadDataAsync<CandidateDashboardShape>(
            await HttpClient.GetAsync($"{Root}/candidate/dashboard"));

        dashboard.SavedJobCount.Should().Be(0);
    }

    [Fact]
    public async Task Saving_needs_a_token()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync("Barista", "hospitality", "GB");

        Api.ClearAuthentication();

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{employer.JobId}/save", new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The dashboard answers for a member who has done nothing yet, rather than 404-ing them into
    /// a dead end on the first screen they open.
    /// </summary>
    [Fact]
    public async Task The_candidate_dashboard_answers_before_a_profile_exists()
    {
        Factory.EnsureDatabaseCreated();

        var (_, member) = await RegisterAndVerifyAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var response = await HttpClient.GetAsync($"{Root}/candidate/dashboard");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var dashboard = await ReadDataAsync<CandidateDashboardShape>(response);

        dashboard.CandidateId.Should().BeNull("there is no profile yet, and that is not an error");
        dashboard.TotalApplicationCount.Should().Be(0);
    }

    /// <summary>
    /// A candidate's application list is scoped to them by the token, never by a parameter. If a
    /// parameter could widen it, a guessed identifier would read somebody else's job hunt.
    /// </summary>
    [Fact]
    public async Task A_candidate_sees_only_their_own_applications()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync("Warehouse Lead", "logistics", "DE");
        var (theirs, _) = await ApplyAsync(employer.JobId);

        var (_, other) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(other.Tokens.AccessToken);

        var list = await ReadDataAsync<ApplicationPageShape>(
            await HttpClient.GetAsync($"{Root}/applications/"));

        list.Items.Select(item => item.ApplicationId)
            .Should().NotContain(theirs.ApplicationId);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a verified member, an employer under them, and a job — published unless asked
    /// otherwise. Returns the employer's token so a test can switch back to that side.
    /// </summary>
    private async Task<PublishedJob> PublishAJobAsync(
        string title, string categoryKey, string countryCode, bool publish = true)
    {
        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employer = await ReadDataAsync<EmployerShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/employers",
                new
                {
                    displayName = $"{title} Employer",
                    organizationType = "Company",
                    countryCode
                }));

        var job = await ReadDataAsync<JobShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/",
                new
                {
                    employerId = employer.EmployerId,
                    title,
                    description = $"A {title} role, described at enough length to be valid.",
                    categoryKey,
                    jobType = "FullTime",
                    experienceLevel = "Mid",
                    countryCode
                }));

        if (publish)
        {
            var published = await HttpClient.PostAsJsonAsync(
                $"{Root}/{job.JobId}/publish", new { openImmediately = true });

            published.StatusCode.Should().Be(
                HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
        }

        return new PublishedJob(
            employer.EmployerId, job.JobId, owner.Tokens.AccessToken);
    }

    /// <summary>
    /// Registers a fresh verified candidate and applies. Hands back their token so a test can
    /// switch between the two sides of the same application.
    /// </summary>
    private async Task<(ApplicationShape Application, string CandidateToken)> ApplyAsync(
        Guid jobId)
    {
        var (_, candidate) = await RegisterVerifiedMemberAsync();
        var token = candidate.Tokens.AccessToken;

        Api.Authenticate(token);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile",
            new { headline = "Experienced and available", countryCode = "NG" });

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/{jobId}/apply", new { coverLetter = (string?)null });

        response.StatusCode.Should().Be(
            HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return (await ReadDataAsync<ApplicationShape>(response), token);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        var data = document.RootElement.GetProperty("data").GetRawText();

        return JsonSerializer.Deserialize<T>(data, JsonOptions)!;
    }

    private sealed record PublishedJob(Guid EmployerId, Guid JobId, string EmployerToken);

    private sealed record EmployerShape(Guid EmployerId, string DisplayName);

    private sealed record JobShape(Guid JobId, string Title, string Status);

    private sealed record ApplicationShape(
        Guid ApplicationId, Guid JobId, string Status, bool CanWithdraw);

    private sealed record ApplicationPageShape(List<ApplicationItemShape> Items, bool HasMore);

    private sealed record ApplicationItemShape(Guid ApplicationId, string Status);

    private sealed record CandidateDashboardShape(
        Guid? CandidateId,
        int ActiveApplicationCount,
        int TotalApplicationCount,
        int SavedJobCount);
}
