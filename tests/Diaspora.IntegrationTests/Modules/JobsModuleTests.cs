using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Jobs &amp; Careers Platform (B9), driven over real HTTP through the same host every other
/// module suite uses.
/// <para>
/// Three claims are under test. That the module composed into the platform through nothing but
/// contributor interfaces — its feature gates turn up in Identity's catalogue, its name in the
/// platform manifest — with no edit to the kernel, to Identity, or to <c>Program.cs</c>. That the
/// gates it inherited from the host's provisional catalogue kept their names and tier floors,
/// because the mobile client already renders against them. And that the B9 rule which decides who
/// may do what actually holds: <b>looking is Limited, being seen is Verified.</b>
/// </para>
/// <para>
/// The wire shapes below are re-declared by hand rather than referenced from the module. Binding
/// the tests to B9's own response records would make them pass whenever the module compiles,
/// which is the one thing a contract test must never do.
/// </para>
/// </summary>
public sealed class JobsModuleTests : IdentityTestBase
{
    private const string Root = "/api/v1/jobs";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JobsModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Proof the module's <c>IFeatureContributor</c> reached the composed catalogue: gates B9
    /// owns turn up in Identity's response, which the Jobs module never touched.
    /// </summary>
    [Fact]
    public async Task The_job_feature_gates_composed_into_the_platform_catalogue()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var names = features.Features.Select(feature => feature.Name).ToList();

        names.Should().Contain("jobs.browse", "browsing is a gate the Jobs module owns");
        names.Should().Contain("jobs.apply", "applying is the module's progression gate");
        names.Should().Contain("jobs.post");
        names.Should().Contain("jobs.employer");
        names.Should().Contain("jobs.recruit");
        names.Should().Contain("jobs.save");
        names.Should().Contain("jobs.profile");
    }

    /// <summary>
    /// <b>The regression this phase most had to avoid.</b> <c>jobs.browse</c> and
    /// <c>jobs.apply</c> were declared by the host's provisional catalogue from B2 onwards. If
    /// both the host and the module declared them the feature catalogue would refuse to compose
    /// and every request that resolves it would fail — including phone verification, which is how
    /// this very test creates its member. Reaching this assertion at all proves the duplicate is
    /// gone; the assertion proves the names and floors survived the move.
    /// </summary>
    [Fact]
    public async Task The_gates_inherited_from_the_provisional_catalogue_kept_their_floors()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        features.Features.Where(feature => feature.Name == "jobs.browse")
            .Should().ContainSingle("two modules declaring one gate would fail composition");

        features.Features.Where(feature => feature.Name == "jobs.apply")
            .Should().ContainSingle();

        features.Features.Single(feature => feature.Name == "jobs.browse")
            .IsEnabled.Should().BeTrue("browsing opens at Limited, as it always did");

        features.Features.Single(feature => feature.Name == "jobs.apply")
            .IsEnabled.Should().BeFalse("applying is floored at Verified, as it always was");
    }

    [Fact]
    public async Task The_applying_gate_opens_for_a_verified_member()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        features.Features.Single(feature => feature.Name == "jobs.apply")
            .IsEnabled.Should().BeTrue("a Verified member sits exactly on the applying floor");
    }

    [Fact]
    public async Task The_platform_manifest_lists_the_jobs_module()
    {
        var response = await HttpClient.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(
            "Jobs", "the kernel discovered the module and published it in the manifest");
    }

    // ── The catalogue seeded ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The employment-domain catalogue arrived through <c>IJobCategoryContributor</c>. Without
    /// it no posting could be drafted, because a category is required.
    /// </summary>
    [Fact]
    public async Task The_employment_domain_catalogue_seeded_at_startup()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await ReadDataAsync<List<CategoryShape>>(response);

        categories.Should().NotBeEmpty("the default contributor seeds fourteen domains");

        categories.Select(category => category.Key)
            .Should().Contain(["healthcare", "construction", "skilled-trades", "technology"],
                "the shipped taxonomy leads with the sectors that employ diaspora members");
    }

    /// <summary>
    /// The catalogue is readable without a token. A member has to see what kinds of work exist
    /// before anybody asks them to sign in.
    /// </summary>
    [Fact]
    public async Task The_catalogue_is_readable_anonymously()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Skill_autocomplete_is_readable_anonymously()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/skills?q=nurs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Public reads ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Browsing carries no authorisation requirement at all — the module's whole point. Somebody
    /// looking for work sees the market before anybody asks who they are.
    /// </summary>
    [Fact]
    public async Task Browsing_jobs_needs_no_token()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/?country=DE");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Searching_jobs_needs_no_token()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/search", new { openOnly = true, limit = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A posting that does not exist and one the caller may not see are the same answer. A 403
    /// on a hidden posting would confirm it exists.
    /// </summary>
    [Fact]
    public async Task An_unknown_job_is_not_found_rather_than_forbidden()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── The tier band ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Registering_an_employer_requires_authentication()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/employers",
            new
            {
                displayName = "Charité Berlin",
                organizationType = "Healthcare",
                countryCode = "DE"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A Limited member may look but not be seen. Registering an employer is a claim about a real
    /// organisation that candidates will act on.
    /// </summary>
    [Fact]
    public async Task A_limited_member_cannot_register_an_employer()
    {
        Factory.EnsureDatabaseCreated();

        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/employers",
            new
            {
                displayName = "Charité Berlin",
                organizationType = "Healthcare",
                countryCode = "DE"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Creating a candidate profile is Limited: building one is private and low-stakes.</summary>
    [Fact]
    public async Task A_limited_member_can_create_a_candidate_profile()
    {
        Factory.EnsureDatabaseCreated();

        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile",
            new { headline = "Registered nurse, 8 years, ICU", countryCode = "NG" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// <b>Private by default.</b> Somebody job-hunting while employed can be dismissed for being
    /// found, so the platform must not make that choice for them.
    /// </summary>
    [Fact]
    public async Task A_new_candidate_profile_is_private()
    {
        Factory.EnsureDatabaseCreated();

        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile",
            new { headline = "Software engineer, 5 years", countryCode = "GH" });

        var profile = await ReadDataAsync<CandidateShape>(response);

        profile.Visibility.Should().Be(
            "Private", "the default must protect the member, not the platform's discovery");
    }

    [Fact]
    public async Task A_member_cannot_create_two_candidate_profiles()
    {
        Factory.EnsureDatabaseCreated();

        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var payload = new { headline = "Care assistant, 3 years", countryCode = "KE" };

        (await HttpClient.PostAsJsonAsync($"{Root}/candidate/profile", payload))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await HttpClient.PostAsJsonAsync($"{Root}/candidate/profile", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Employer registration and posting ───────────────────────────────────────────────────

    [Fact]
    public async Task A_verified_member_can_register_an_employer()
    {
        Factory.EnsureDatabaseCreated();

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/employers",
            new
            {
                displayName = "Charité Berlin",
                legalName = "Charité — Universitätsmedizin Berlin",
                organizationType = "Healthcare",
                countryCode = "DE"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var employer = await ReadDataAsync<EmployerShape>(response);

        employer.DisplayName.Should().Be("Charité Berlin");
        employer.IsVerified.Should().BeFalse(
            "registering is not verifying — only Trust can grant that badge");
        employer.Slug.Should().NotBeNullOrWhiteSpace("a deep-link handle is allocated");
    }

    /// <summary>
    /// The whole vertical: register, draft, publish, and find it in a public search — driven
    /// over HTTP with no shortcuts into the domain.
    /// </summary>
    [Fact]
    public async Task An_employer_can_draft_and_publish_a_job_that_then_appears_publicly()
    {
        Factory.EnsureDatabaseCreated();

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var employer = await ReadDataAsync<EmployerShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/employers",
                new
                {
                    displayName = "Berlin Care Group",
                    organizationType = "Healthcare",
                    countryCode = "DE"
                }));

        var created = await HttpClient.PostAsJsonAsync(
            $"{Root}/",
            new
            {
                employerId = employer.EmployerId,
                title = "Registered Nurse — Night Shift",
                description = "Ward nursing on a busy acute unit in central Berlin.",
                categoryKey = "healthcare",
                jobType = "FullTime",
                experienceLevel = "Mid",
                countryCode = "DE",
                cityName = "Berlin",
                workplaceType = "OnSite",
                offersVisaSponsorship = true
            });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var job = await ReadDataAsync<JobShape>(created);

        job.Status.Should().Be("Draft", "a posting is never born live");
        job.CanApply.Should().BeFalse();

        var published = await HttpClient.PostAsJsonAsync(
            $"{Root}/{job.JobId}/publish", new { openImmediately = true });

        published.StatusCode.Should().Be(HttpStatusCode.OK);

        var live = await ReadDataAsync<JobShape>(published);

        live.Status.Should().Be("Open");
        live.CanApply.Should().BeTrue();

        // Now anonymously, through the public search.
        Api.ClearAuthentication();

        var found = await ReadDataAsync<SearchShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/search", new { countryCode = "DE", openOnly = true, limit = 50 }));

        found.Items.Select(item => item.JobId).Should().Contain(
            job.JobId, "a published job is public and needs no token to find");

        // And through the shareable GET, which is a separate binding and cursor path. Both are
        // asserted with a row actually present: the read path that broke in development was
        // fine against an empty table and threw the moment there was something to return.
        var browse = await HttpClient.GetAsync($"{Root}/?country=DE");

        browse.StatusCode.Should().Be(
            HttpStatusCode.OK, await browse.Content.ReadAsStringAsync());

        var browsed = await ReadDataAsync<SearchShape>(browse);

        browsed.Items.Select(item => item.JobId).Should().Contain(job.JobId);
    }

    /// <summary>
    /// A draft belongs to its employer alone. Anyone else gets not-found, which does not confirm
    /// it exists.
    /// </summary>
    [Fact]
    public async Task A_draft_is_invisible_to_everybody_else()
    {
        Factory.EnsureDatabaseCreated();

        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employer = await ReadDataAsync<EmployerShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/employers",
                new
                {
                    displayName = "Quiet Hiring Ltd",
                    organizationType = "Company",
                    countryCode = "GB"
                }));

        var job = await ReadDataAsync<JobShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/",
                new
                {
                    employerId = employer.EmployerId,
                    title = "Confidential Search",
                    description = "A role we are not advertising yet.",
                    categoryKey = "technology",
                    jobType = "FullTime",
                    experienceLevel = "Senior",
                    countryCode = "GB"
                }));

        Api.ClearAuthentication();

        var anonymous = await HttpClient.GetAsync($"{Root}/{job.JobId}");

        anonymous.StatusCode.Should().Be(
            HttpStatusCode.NotFound, "a 403 would confirm the draft exists");
    }

    /// <summary>
    /// A recruiter at one employer has no authority at another. This is the second
    /// authorisation question — membership — which no permission on a token can answer.
    /// </summary>
    [Fact]
    public async Task A_member_of_one_employer_cannot_post_under_another()
    {
        Factory.EnsureDatabaseCreated();

        var (_, first) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(first.Tokens.AccessToken);

        var theirEmployer = await ReadDataAsync<EmployerShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/employers",
                new
                {
                    displayName = "First Employer",
                    organizationType = "Company",
                    countryCode = "DE"
                }));

        // A different verified member, with no membership of the employer above.
        var (_, second) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(second.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/",
            new
            {
                employerId = theirEmployer.EmployerId,
                title = "Not Mine To Post",
                description = "Posting under somebody else's employer.",
                categoryKey = "technology",
                jobType = "FullTime",
                experienceLevel = "Mid",
                countryCode = "DE"
            });

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the policy let them create jobs; the aggregate decides they may not create this one");
    }

    // ── Envelope and problem shapes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Job_responses_use_the_platform_success_envelope()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/categories");
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        document.RootElement.TryGetProperty("data", out _).Should().BeTrue(
            "every module returns the same envelope");
        document.RootElement.TryGetProperty("meta", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Job_errors_use_the_platform_problem_document()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/{Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        document.RootElement.TryGetProperty("title", out _).Should().BeTrue();
        document.RootElement.TryGetProperty("status", out _).Should().BeTrue();

        body.Should().Contain(
            "jobs.posting.not-found", "the wire error code is a frozen contract");
    }

    // ── Wire shapes, re-declared by hand ────────────────────────────────────────────────────

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        var data = document.RootElement.GetProperty("data").GetRawText();

        return JsonSerializer.Deserialize<T>(data, JsonOptions)!;
    }

    private sealed record CategoryShape(string Key, string Name, bool IsActive);

    private sealed record CandidateShape(Guid CandidateId, string Headline, string Visibility);

    private sealed record EmployerShape(
        Guid EmployerId, string Slug, string DisplayName, bool IsVerified);

    private sealed record JobShape(
        Guid JobId, string Slug, string Title, string Status, bool CanApply, bool CanManage);

    private sealed record SearchShape(List<SearchItemShape> Items, bool HasMore);

    private sealed record SearchItemShape(Guid JobId, string Title, string Status);
}
