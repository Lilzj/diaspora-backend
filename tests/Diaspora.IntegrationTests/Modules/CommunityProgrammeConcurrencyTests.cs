using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B18's concurrency guarantees, against a host that can actually serve two requests at once.
/// <para>
/// <b>These are in their own fixture because the suite's ordinary host cannot run them.</b> Every
/// other module test shares one <c>SqliteConnection</c>, and two requests arriving together make the
/// driver throw from <c>SqliteConnection.RemoveCommand</c> inside the authentication middleware —
/// a 500 before the request reaches any module. See <see cref="ConcurrentWebApplicationFactory"/>.
/// </para>
/// <para>
/// <b>What is under test is the database constraint, not the handler's lookup.</b> Both handlers look
/// their idempotency key up before writing, and two concurrent requests both find nothing and both
/// proceed — which is the point. What stops a second membership and a second vote is the unique
/// index, and what turns the resulting violation into an answer rather than a 500 is the idempotency
/// guard. A test that only proved the lookup works would prove the easy half.
/// </para>
/// </summary>
public sealed class CommunityProgrammeConcurrencyTests
    : IdentityTestBase<ConcurrentWebApplicationFactory>
{
    private const string Root = "/api/v1/communities";
    private const string CommunityLife = "/api/v1/community-life";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityProgrammeConcurrencyTests(ConcurrentWebApplicationFactory factory)
        : base(factory) { }

    /// <summary>
    /// <b>Two concurrent applications from one member produce one membership.</b> One request wins;
    /// the other either replays it or reports the first as still in flight, and neither answer is a
    /// second membership.
    /// </summary>
    [Fact]
    public async Task Concurrent_applications_from_one_member_produce_one_membership()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId);
        var tier = programme.Tiers[0].TierId;

        var (_, applicant) = await RegisterVerifiedMemberAsync();

        using var first = Client(applicant.Tokens.AccessToken);
        using var second = Client(applicant.Tokens.AccessToken);

        var payload = new { tierId = tier, idempotencyKey = Guid.NewGuid().ToString("N") };
        var path = $"{Root}/{organizationId}/memberships/applications";

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync(path, payload),
            second.PostAsJsonAsync(path, payload));

        responses.Should().Contain(response => response.IsSuccessStatusCode);

        responses.Should().OnlyContain(response =>
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict);

        Api.Authenticate(applicant.Tokens.AccessToken);

        var mine = await ReadDataAsync<List<MembershipDto>>(
            await HttpClient.GetAsync($"{Root}/memberships/me"));

        mine.Count(row => row.OrganizationId == organizationId).Should().Be(
            1, "the unique index on (organization, member) is what makes this true");
    }

    /// <summary>
    /// <b>Two concurrent applications with different keys still produce one membership.</b> The
    /// idempotency key is not what enforces this — the unique index is — and a member who taps twice
    /// on two devices generates two different keys.
    /// </summary>
    [Fact]
    public async Task Concurrent_applications_with_different_keys_still_produce_one_membership()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId);
        var tier = programme.Tiers[0].TierId;

        var (_, applicant) = await RegisterVerifiedMemberAsync();

        using var first = Client(applicant.Tokens.AccessToken);
        using var second = Client(applicant.Tokens.AccessToken);

        var path = $"{Root}/{organizationId}/memberships/applications";

        await Task.WhenAll(
            first.PostAsJsonAsync(
                path, new { tierId = tier, idempotencyKey = Guid.NewGuid().ToString("N") }),
            second.PostAsJsonAsync(
                path, new { tierId = tier, idempotencyKey = Guid.NewGuid().ToString("N") }));

        Api.Authenticate(applicant.Tokens.AccessToken);

        var mine = await ReadDataAsync<List<MembershipDto>>(
            await HttpClient.GetAsync($"{Root}/memberships/me"));

        mine.Count(row => row.OrganizationId == organizationId).Should().Be(1);
    }

    /// <summary>
    /// <b>Two concurrent votes from one member produce one vote.</b> One member, one vote, enforced by
    /// the unique index on (poll, fingerprint) rather than by a check two devices would both pass.
    /// </summary>
    [Fact]
    public async Task Concurrent_votes_from_one_member_produce_one_vote()
    {
        await AuthenticateAsync();
        var poll = await CreatePollAsync();

        var (_, voter) = await RegisterVerifiedMemberAsync();

        using var first = Client(voter.Tokens.AccessToken);
        using var second = Client(voter.Tokens.AccessToken);

        var payload = new { selectedOptionIds = new[] { poll.Options[0].OptionId } };
        var path = $"{Root}/polls/{poll.PollId}/responses";

        await Task.WhenAll(
            first.PostAsJsonAsync(path, payload),
            second.PostAsJsonAsync(path, payload));

        Api.Authenticate(voter.Tokens.AccessToken);

        var result = await ReadDataAsync<PollResultDto>(
            await HttpClient.GetAsync($"{Root}/polls/{poll.PollId}/results"));

        result.TotalResponses.Should().Be(1, "one member, one vote");

        result.Options!.Sum(option => option.Count).Should().Be(
            1, "and the tally counts it once");
    }

    /// <summary>
    /// <b>Two members voting at once both count.</b> The negative control: without it, a test that
    /// asserted "one vote" would also pass against a poll that lost every concurrent write.
    /// </summary>
    [Fact]
    public async Task Concurrent_votes_from_two_members_both_count()
    {
        await AuthenticateAsync();
        var poll = await CreatePollAsync();

        var (_, one) = await RegisterVerifiedMemberAsync();
        var (_, two) = await RegisterVerifiedMemberAsync();

        using var first = Client(one.Tokens.AccessToken);
        using var second = Client(two.Tokens.AccessToken);

        var path = $"{Root}/polls/{poll.PollId}/responses";

        await Task.WhenAll(
            first.PostAsJsonAsync(
                path, new { selectedOptionIds = new[] { poll.Options[0].OptionId } }),
            second.PostAsJsonAsync(
                path, new { selectedOptionIds = new[] { poll.Options[1].OptionId } }));

        Api.Authenticate(one.Tokens.AccessToken);

        var result = await ReadDataAsync<PollResultDto>(
            await HttpClient.GetAsync($"{Root}/polls/{poll.PollId}/results"));

        result.TotalResponses.Should().Be(2);
        result.Options!.Should().OnlyContain(option => option.Count == 1);
    }

    /// <summary>
    /// <b>Two organizers assigning one applicant at once produce one assignment.</b> The unique index
    /// on the application is what makes a double-click harmless.
    /// </summary>
    [Fact]
    public async Task Concurrent_assignments_of_one_applicant_produce_one_assignment()
    {
        var organizerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var opportunity = await CreateOpportunityAsync(organizationId);

        await AuthenticateAsync();

        var application = await ReadDataAsync<ApplicationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/opportunities/{opportunity.OpportunityId}/applications",
                new { shiftId = opportunity.Shifts[0].ShiftId }));

        Api.Authenticate(organizerToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/volunteer/applications/{application.ApplicationId}/decision",
            new { decision = "approve" });

        using var first = Client(organizerToken);
        using var second = Client(organizerToken);

        var payload = new
        {
            applicationId = application.ApplicationId,
            shiftId = opportunity.Shifts[0].ShiftId
        };

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync($"{Root}/volunteer/assignments", payload),
            second.PostAsJsonAsync($"{Root}/volunteer/assignments", payload));

        responses.Should().Contain(response => response.IsSuccessStatusCode);

        var assignments = await ReadDataAsync<List<AssignmentDto>>(
            await HttpClient.GetAsync($"{Root}/volunteer/assignments/me"));

        assignments.Count(row => row.ApplicationId == application.ApplicationId)
            .Should().BeLessThanOrEqualTo(1, "an organizer's second click is not a second placement");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private HttpClient Client(string token)
    {
        var client = Factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private async Task<string> AuthenticateAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        return auth.Tokens.AccessToken;
    }

    private async Task<Guid> CreateOrganizationAsync()
    {
        var created = await ReadDataAsync<OrganizationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{CommunityLife}/organizations",
                new
                {
                    name = $"Community Association {Guid.NewGuid():N}",
                    summary = "A body created by the integration suite.",
                    organizationTypeKey = "cultural-association",
                    countryCode = "GB",
                    cityName = "London"
                }));

        return created.OrganizationId;
    }

    private async Task<ProgrammeDto> SaveProgrammeAsync(Guid organizationId) =>
        await ReadDataAsync<ProgrammeDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Root}/{organizationId}/membership",
                new
                {
                    summary = "Join us.",
                    visibility = "Public",
                    isOpen = true,
                    requiresApproval = true,
                    tiers = new[]
                    {
                        new
                        {
                            name = "Supporter",
                            description = "The free one.",
                            billingPeriod = "Yearly",
                            sortOrder = 0
                        }
                    }
                }));

    private async Task<OpportunityDto> CreateOpportunityAsync(Guid organizationId)
    {
        var start = DateTimeOffset.UtcNow.AddDays(14);

        return await ReadDataAsync<OpportunityDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/opportunities",
                new
                {
                    organizationId,
                    title = "Foodbank stewards",
                    description = "Help us run the foodbank.",
                    cause = "Food poverty",
                    countryCode = "GB",
                    cityName = "London",
                    startsAt = start,
                    endsAt = start.AddHours(4),
                    shifts = new[]
                    {
                        new
                        {
                            label = "Morning",
                            startsAt = start,
                            endsAt = start.AddHours(4),
                            capacity = 4
                        }
                    },
                    publish = true
                }));
    }

    private async Task<PollDto> CreatePollAsync() =>
        await ReadDataAsync<PollDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/polls",
                new
                {
                    question = "Should we move the meeting?",
                    votingType = "SingleChoice",
                    audience = "Public",
                    options = new[] { new { label = "Yes" }, new { label = "No" } },
                    isAnonymous = false,
                    allowsChange = false,
                    showResultsBeforeClose = true,
                    publish = true
                }));

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded, but answered {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    // ── Wire shapes ─────────────────────────────────────────────────────────────────────────

    private sealed record OrganizationDto(Guid OrganizationId);

    private sealed record TierDto(Guid TierId, string Name);

    private sealed record ProgrammeDto(Guid OrganizationId, List<TierDto> Tiers);

    private sealed record MembershipDto(Guid MembershipId, Guid OrganizationId, string Status);

    private sealed record ShiftDto(Guid ShiftId, int Capacity);

    private sealed record OpportunityDto(Guid OpportunityId, List<ShiftDto> Shifts);

    private sealed record ApplicationDto(Guid ApplicationId, string Status);

    private sealed record AssignmentDto(Guid AssignmentId, Guid ApplicationId, string Status);

    private sealed record PollOptionDto(Guid OptionId, string Label);

    private sealed record PollDto(Guid PollId, List<PollOptionDto> Options);

    private sealed record PollOptionResultDto(Guid OptionId, int Count);

    private sealed record PollResultDto(
        Guid PollId, List<PollOptionResultDto>? Options, int TotalResponses);
}
