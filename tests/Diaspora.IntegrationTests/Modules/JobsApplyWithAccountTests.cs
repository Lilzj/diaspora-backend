using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Applying for a job with a Diaspora account alone — 14 Sep 2026.
/// <para>
/// <b>Found by a product owner watching a walkthrough.</b> A signed-in member tapped Apply and was
/// told to "create your profile first". The member already had a profile: their account. What the
/// app wanted was a Jobs candidate profile with a headline, because the apply route refused
/// anyone without one — and the employer, once they finally received the application, saw that
/// headline and not the member's name.
/// </para>
/// <para>
/// So the refusal is gone rather than smoothed over. A first application makes the candidate
/// profile, private and empty, in the same commit as the application; the employer's list names
/// the applicant from their account and says whether that account is verified now. These tests go
/// through HTTP, because the defect was a join between the route, the profile and the list, and
/// each of those was tested on its own already.
/// </para>
/// </summary>
public sealed class JobsApplyWithAccountTests : IdentityTestBase
{
    private const string Root = "/api/v1/jobs";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JobsApplyWithAccountTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_verified_member_applies_without_writing_a_candidate_profile_first()
    {
        Factory.EnsureDatabaseCreated();

        var job = await PublishAJobAsync("Care Assistant");

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var applied = await HttpClient.PostAsJsonAsync(
            $"{Root}/{job.JobId}/apply", new { coverLetter = (string?)null });

        applied.StatusCode.Should().Be(
            HttpStatusCode.Created, await applied.Content.ReadAsStringAsync());

        // The profile the application made: theirs, private, and saying nothing they did not write.
        var profile = await HttpClient.GetAsync($"{Root}/candidate/profile");
        profile.StatusCode.Should().Be(HttpStatusCode.OK, await profile.Content.ReadAsStringAsync());

        var shape = await ReadDataAsync<CandidateProfileShape>(profile);
        shape.UserId.Should().Be(member.User.Id);
        shape.Headline.Should().BeNull("nobody wrote one, and an empty string is not a headline");
        shape.Visibility.Should().Be("Private");
    }

    [Fact]
    public async Task A_second_application_uses_the_profile_the_first_one_made()
    {
        Factory.EnsureDatabaseCreated();

        var first = await PublishAJobAsync("Kitchen Porter");
        var second = await PublishAJobAsync("Night Porter");

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        (await HttpClient.PostAsJsonAsync($"{Root}/{first.JobId}/apply", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var again = await HttpClient.PostAsJsonAsync($"{Root}/{second.JobId}/apply", new { });
        again.StatusCode.Should().Be(HttpStatusCode.Created, await again.Content.ReadAsStringAsync());

        var mine = await ReadDataAsync<ApplicationPageShape>(
            await HttpClient.GetAsync($"{Root}/applications/"));

        mine.Items.Should().HaveCount(2);
        mine.Items.Select(item => item.CandidateId).Distinct().Should().ContainSingle(
            "one member has one candidate profile, however many jobs they apply for");
    }

    [Fact]
    public async Task An_application_that_is_refused_leaves_no_profile_behind()
    {
        Factory.EnsureDatabaseCreated();

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var refused = await HttpClient.PostAsJsonAsync($"{Root}/{Guid.NewGuid()}/apply", new { });
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The profile is made in the application's commit, so a refusal commits neither.
        (await HttpClient.GetAsync($"{Root}/candidate/profile")).StatusCode
            .Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_employer_sees_who_applied_by_their_account_name_and_verified_status()
    {
        Factory.EnsureDatabaseCreated();

        var job = await PublishAJobAsync("Ward Clerk");

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        (await HttpClient.PostAsJsonAsync($"{Root}/{job.JobId}/apply", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        Api.Authenticate(job.EmployerToken);

        var list = await ReadDataAsync<ApplicationPageShape>(
            await HttpClient.GetAsync($"{Root}/applications/?employerId={job.EmployerId}"));

        var row = list.Items.Should().ContainSingle().Subject;
        row.ApplicantName.Should().Be(member.User.FullName);
        row.ApplicantIsVerified.Should().BeTrue();
        row.CandidateHeadline.Should().BeNull("the applicant wrote no headline");
    }

    [Fact]
    public async Task An_applicant_whose_verification_is_withdrawn_is_no_longer_shown_verified()
    {
        Factory.EnsureDatabaseCreated();

        var job = await PublishAJobAsync("Receptionist");

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        (await HttpClient.PostAsJsonAsync($"{Root}/{job.JobId}/apply", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // The badge is the account's present state, not a record of the day they applied.
        (await ApplyTrustDecisionAsync(member.User.Id, "Revoked")).IsSuccessStatusCode.Should().BeTrue();

        Api.Authenticate(job.EmployerToken);

        var list = await ReadDataAsync<ApplicationPageShape>(
            await HttpClient.GetAsync($"{Root}/applications/?employerId={job.EmployerId}"));

        list.Items.Should().ContainSingle().Which.ApplicantIsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task A_candidate_is_not_shown_their_own_name_on_their_own_tracker()
    {
        Factory.EnsureDatabaseCreated();

        var job = await PublishAJobAsync("Porter");

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        (await HttpClient.PostAsJsonAsync($"{Root}/{job.JobId}/apply", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var mine = await ReadDataAsync<ApplicationPageShape>(
            await HttpClient.GetAsync($"{Root}/applications/"));

        var row = mine.Items.Should().ContainSingle().Subject;
        row.ApplicantName.Should().BeNull();
        row.ApplicantIsVerified.Should().BeNull();
    }

    [Fact]
    public async Task A_member_can_still_fill_a_profile_in_first_without_a_headline_and_keep_their_city()
    {
        Factory.EnsureDatabaseCreated();

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var created = await HttpClient.PostAsJsonAsync(
            $"{Root}/candidate/profile", new { countryCode = "MU", cityName = "Curepipe" });

        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var shape = await ReadDataAsync<CandidateProfileShape>(created);
        shape.Headline.Should().BeNull();
        shape.CityName.Should().Be("Curepipe", "the create request always carried a city and nothing kept it");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A verified member, an employer under them, and a published job.</summary>
    private async Task<PublishedJob> PublishAJobAsync(string title)
    {
        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employer = await ReadDataAsync<EmployerShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/employers",
                new { displayName = $"{title} Employer", organizationType = "Company", countryCode = "DE" }));

        var job = await ReadDataAsync<JobShape>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/",
                new
                {
                    employerId = employer.EmployerId,
                    title,
                    description = $"A {title} role, described at enough length to be valid.",
                    categoryKey = "healthcare",
                    jobType = "FullTime",
                    experienceLevel = "Mid",
                    countryCode = "DE"
                }));

        var published = await HttpClient.PostAsJsonAsync(
            $"{Root}/{job.JobId}/publish", new { openImmediately = true });

        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        return new PublishedJob(employer.EmployerId, job.JobId, owner.Tokens.AccessToken);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        return JsonSerializer.Deserialize<T>(
            document.RootElement.GetProperty("data").GetRawText(), JsonOptions)!;
    }

    private sealed record PublishedJob(Guid EmployerId, Guid JobId, string EmployerToken);

    private sealed record EmployerShape(Guid EmployerId);

    private sealed record JobShape(Guid JobId);

    private sealed record CandidateProfileShape(
        Guid UserId, string? Headline, string? CityName, string Visibility);

    private sealed record ApplicationPageShape(List<ApplicationRowShape> Items);

    private sealed record ApplicationRowShape(
        Guid ApplicationId,
        Guid CandidateId,
        string? CandidateHeadline,
        string? ApplicantName,
        bool? ApplicantIsVerified);
}
