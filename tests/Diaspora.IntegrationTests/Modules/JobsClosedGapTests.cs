using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The five read routes the Final Runtime Gap Closure phase confirmed were missing, now present.
/// <para>
/// Each was probed against the running API before it existed, and each answered **404 or 405** —
/// a resource path published for writes only. The evidence is in
/// `runtime-remediation/e2e-completion/re-derived-backend-gaps.md` as gaps G-1 … G-5, and the
/// symptom in each case was the same shape: the platform wrote data it gave nobody a way to read.
/// </para>
/// <para>
/// The assertions that matter here are the refusals, not the happy paths. An interview names a
/// time and a joining link; an offer names a salary. Both are readable by exactly two parties, and
/// a third must get nothing — and must not be able to learn from the refusal that the application
/// exists at all.
/// </para>
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class JobsClosedGapTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/jobs";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JobsClosedGapTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ══ G-3: the interview list ═════════════════════════════════════════════════════════════

    [Fact]
    public async Task Both_sides_of_the_table_can_read_the_interview_rounds()
    {
        Factory.EnsureDatabaseCreated();

        var hiring = await ArrangeApplicationAsync();

        // The employer schedules a round.
        Api.Authenticate(hiring.EmployerToken);

        var scheduled = await HttpClient.PostAsJsonAsync(
            $"{Root}/applications/{hiring.ApplicationId}/interviews",
            new
            {
                mode = "Video",
                scheduledAt = DateTimeOffset.UtcNow.AddDays(4),
                durationMinutes = 45,
                joiningUrl = "https://meet.example.com/round-one",
                title = "Systems design"
            });

        scheduled.StatusCode.Should().Be(
            HttpStatusCode.Created, await scheduled.Content.ReadAsStringAsync());

        // The employer reads it back — this is the route that did not exist.
        var forEmployer = await HttpClient.GetAsync(
            $"{Root}/applications/{hiring.ApplicationId}/interviews");

        forEmployer.StatusCode.Should().Be(HttpStatusCode.OK);
        var employerRounds = await ReadDataAsync<List<InterviewShape>>(forEmployer);
        employerRounds.Should().HaveCount(1);
        employerRounds[0].RoundNumber.Should().Be(1);

        // And so does the candidate, who is the person expected to turn up to it.
        Api.Authenticate(hiring.CandidateToken);

        var forCandidate = await HttpClient.GetAsync(
            $"{Root}/applications/{hiring.ApplicationId}/interviews");

        forCandidate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<List<InterviewShape>>(forCandidate)).Should().HaveCount(1);
    }

    /// <summary>
    /// An application with no interviews is an ordinary state. The employer dashboard's
    /// <c>upcomingInterviewCount</c> can legitimately be zero, and the list must agree rather than
    /// answer "not found".
    /// </summary>
    [Fact]
    public async Task An_application_with_no_interviews_answers_with_an_empty_list()
    {
        Factory.EnsureDatabaseCreated();

        var hiring = await ArrangeApplicationAsync();
        Api.Authenticate(hiring.EmployerToken);

        var response = await HttpClient.GetAsync(
            $"{Root}/applications/{hiring.ApplicationId}/interviews");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<List<InterviewShape>>(response)).Should().BeEmpty();
    }

    // ══ G-4: the offer list ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_candidate_can_read_the_terms_before_accepting_them()
    {
        Factory.EnsureDatabaseCreated();

        var hiring = await ArrangeApplicationAsync();
        Api.Authenticate(hiring.EmployerToken);

        var extended = await HttpClient.PostAsJsonAsync(
            $"{Root}/applications/{hiring.ApplicationId}/offers",
            new
            {
                salaryAmount = 9_600_000m,
                currencyCode = "NGN",
                salaryPeriod = "Yearly",
                expiresAt = DateTimeOffset.UtcNow.AddDays(10)
            });

        extended.StatusCode.Should().Be(
            HttpStatusCode.Created, await extended.Content.ReadAsStringAsync());

        // The whole point of the gap: the candidate could accept an offer they had no route to
        // read. Now they can read it first.
        Api.Authenticate(hiring.CandidateToken);

        var response = await HttpClient.GetAsync(
            $"{Root}/applications/{hiring.ApplicationId}/offers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var offers = await ReadDataAsync<List<OfferShape>>(response);
        offers.Should().HaveCount(1);
        offers[0].OfferId.Should().NotBeEmpty();
    }

    // ══ The refusals, which are the reason these are read routes and not open ones ══════════

    [Fact]
    public async Task A_stranger_cannot_read_the_interviews_on_somebody_elses_application()
    {
        Factory.EnsureDatabaseCreated();

        var hiring = await ArrangeApplicationAsync();

        var (_, stranger) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(stranger.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            $"{Root}/applications/{hiring.ApplicationId}/interviews");

        // NotFound rather than Forbidden, deliberately: 403 on a guessable identifier confirms
        // that an application exists between two named parties, which is itself a disclosure.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_stranger_cannot_read_the_offers_on_somebody_elses_application()
    {
        Factory.EnsureDatabaseCreated();

        var hiring = await ArrangeApplicationAsync();

        var (_, stranger) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(stranger.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            $"{Root}/applications/{hiring.ApplicationId}/offers");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ══ G-1 / G-2: the employer public profile ══════════════════════════════════════════════

    [Fact]
    public async Task An_employer_profile_can_be_opened_by_somebody_deciding_whether_to_apply()
    {
        Factory.EnsureDatabaseCreated();

        var job = await PublishAJobAsync("Ward Nurse", "healthcare", "DE");

        // A member who has nothing to do with this employer — which is who reads a job advert.
        var (_, reader) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reader.Tokens.AccessToken);

        var response = await HttpClient.GetAsync($"{Root}/employers/{job.EmployerId}");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var employer = await ReadDataAsync<EmployerProfileShape>(response);
        employer.EmployerId.Should().Be(job.EmployerId);
        employer.DisplayName.Should().NotBeNullOrWhiteSpace();
        employer.Slug.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// A public profile is linkable, so the slug has to resolve as well as the identifier.
    /// </summary>
    [Fact]
    public async Task An_employer_profile_resolves_by_slug_as_well_as_by_id()
    {
        Factory.EnsureDatabaseCreated();

        var job = await PublishAJobAsync("Site Engineer", "healthcare", "GB");
        Api.Authenticate(job.EmployerToken);

        var slug = (await ReadDataAsync<EmployerProfileShape>(
            await HttpClient.GetAsync($"{Root}/employers/{job.EmployerId}"))).Slug;

        var bySlug = await HttpClient.GetAsync($"{Root}/employers/{slug}");

        bySlug.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<EmployerProfileShape>(bySlug)).EmployerId.Should().Be(job.EmployerId);
    }

    /// <summary>
    /// The new parameter route must not shadow the literal one. ASP.NET Core matches by segment
    /// specificity rather than registration order, and this is the test that says so out loud.
    /// </summary>
    [Fact]
    public async Task The_employer_dashboard_is_not_shadowed_by_the_profile_route()
    {
        Factory.EnsureDatabaseCreated();

        var job = await PublishAJobAsync("Line Cook", "healthcare", "GB");
        Api.Authenticate(job.EmployerToken);

        var response = await HttpClient.GetAsync($"{Root}/employers/dashboard");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_employer_that_does_not_exist_answers_not_found()
    {
        Factory.EnsureDatabaseCreated();

        var (_, reader) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reader.Tokens.AccessToken);

        var response = await HttpClient.GetAsync($"{Root}/employers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ══ G-5: reading your own candidate profile ═════════════════════════════════════════════

    [Fact]
    public async Task A_candidate_can_read_back_the_profile_they_created()
    {
        Factory.EnsureDatabaseCreated();

        var (_, candidate) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(candidate.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile",
            new { headline = "Registered nurse, 8 years, ICU", countryCode = "NG" });

        var response = await HttpClient.GetAsync($"{Root}/candidate/profile");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var profile = await ReadDataAsync<CandidateProfileShape>(response);
        profile.Headline.Should().Be("Registered nurse, 8 years, ICU");
    }

    /// <summary>
    /// A member who has never applied for anything has no profile, and the route says so rather
    /// than inventing an empty one — which would make the client unable to tell "not set up yet"
    /// from "set up and blank".
    /// </summary>
    [Fact]
    public async Task A_member_with_no_candidate_profile_is_told_so_rather_than_given_a_blank_one()
    {
        Factory.EnsureDatabaseCreated();

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var response = await HttpClient.GetAsync($"{Root}/candidate/profile");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    // ── Arrangement ─────────────────────────────────────────────────────────────────────────

    /// <summary>An employer, a published job, and a candidate who has applied to it.</summary>
    private async Task<HiringPair> ArrangeApplicationAsync()
    {
        var job = await PublishAJobAsync("Ward Nurse", "healthcare", "DE");

        var (_, candidate) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(candidate.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile",
            new { headline = "Registered nurse, 8 years, ICU", countryCode = "NG" });

        var applied = await HttpClient.PostAsJsonAsync(
            $"{Root}/{job.JobId}/apply",
            new { coverLetter = "I have eight years on acute wards and I am ready to relocate." });

        applied.StatusCode.Should().Be(
            HttpStatusCode.Created, await applied.Content.ReadAsStringAsync());

        var application = await ReadDataAsync<ApplicationIdShape>(applied);

        // Shortlist before returning. An application cannot move straight from `Submitted` to
        // `InterviewScheduled` — `jobs.application.transition-invalid` — and an offer cannot be
        // extended against one nobody has looked at. The pipeline is ordered on purpose, and a
        // fixture that skipped it would be testing a state the product never reaches.
        Api.Authenticate(job.EmployerToken);

        var decided = await HttpClient.PostAsJsonAsync(
            $"{Root}/applications/{application.ApplicationId}/decide",
            new { status = "Shortlisted", reason = "Relevant experience." });

        decided.StatusCode.Should().Be(
            HttpStatusCode.OK, await decided.Content.ReadAsStringAsync());

        return new HiringPair(
            job.EmployerId,
            application.ApplicationId,
            job.EmployerToken,
            candidate.Tokens.AccessToken);
    }

    private async Task<PublishedJob> PublishAJobAsync(
        string title, string categoryKey, string countryCode)
    {
        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employer = await ReadDataAsync<EmployerProfileShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/employers",
                new { displayName = $"{title} Employer", organizationType = "Company", countryCode }));

        var job = await ReadDataAsync<JobIdShape>(
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

        var published = await HttpClient.PostAsJsonAsync(
            $"{Root}/{job.JobId}/publish", new { openImmediately = true });

        published.StatusCode.Should().Be(
            HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        return new PublishedJob(employer.EmployerId, job.JobId, owner.Tokens.AccessToken);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record HiringPair(
        Guid EmployerId, Guid ApplicationId, string EmployerToken, string CandidateToken);

    private sealed record PublishedJob(Guid EmployerId, Guid JobId, string EmployerToken);

    private sealed record EmployerProfileShape(Guid EmployerId, string DisplayName, string Slug);

    private sealed record JobIdShape(Guid JobId);

    private sealed record ApplicationIdShape(Guid ApplicationId);

    private sealed record InterviewShape(Guid InterviewId, int RoundNumber);

    private sealed record OfferShape(Guid OfferId);

    private sealed record CandidateProfileShape(Guid CandidateId, string Headline);
}
