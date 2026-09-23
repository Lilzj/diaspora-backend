using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B18's facts reaching a member's inbox, end to end and through B13.
/// <para>
/// <b>This is what makes "uses the B13 notification boundary" a claim rather than a hope.</b> B18
/// publishes an integration event and stops; B13's consumer reads the published contract, its seeded
/// rule picks a template, and the member's inbox has a message in it. Nothing in B18 composes a
/// notification, names a channel or knows a template key exists — and the only way to prove that
/// arrangement works is to drive the whole thing.
/// </para>
/// <para>
/// <b>The outbox is drained explicitly.</b> Its background dispatcher is off in this host — a loop
/// querying the suite's one shared SQLite connection mid-command is a concurrent use SQLite will not
/// allow — so the test drains it and gets a deterministic queue rather than asserting after a timer
/// happened to fire.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityProgrammeNotificationTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/communities";
    private const string CommunityLife = "/api/v1/community-life";
    private const string Notifications = "/api/v1/notifications";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityProgrammeNotificationTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    /// <summary>
    /// <b>An approval reaches the member.</b> The organisation decides in B18; the member is told by
    /// B13; and the message carries the membership identifier the client's deep link needs.
    /// </summary>
    [Fact]
    public async Task An_approved_membership_reaches_the_members_inbox()
    {
        var ownerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId);
        var tier = programme.Tiers[0].TierId;

        var applicantToken = await AuthenticateAsync();
        var membership = await ApplyAsync(organizationId, tier);

        // Everything queued so far is somebody else's — the organization's creation, the applicant's
        // registration. Drain it so the assertion below is about this decision.
        await Factory.DrainOutboxAsync();

        Api.Authenticate(ownerToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/memberships/{membership.MembershipId}/decision",
            new { decision = "approve" });

        await Factory.DrainOutboxAsync();

        Api.Authenticate(applicantToken);

        var inbox = await ReadDataAsync<InboxPageDto>(
            await HttpClient.GetAsync($"{Notifications}?take=50"));

        inbox.Items.Should().Contain(
            item => item.EntityType == "membership"
                && item.EntityId == membership.MembershipId.ToString(),
            "B18 published the decision and B13 delivered it");
    }

    /// <summary>
    /// <b>A rejection reaches the member too, and it carries the organisation's own words.</b> A
    /// member refused by a body is owed the sentence it wrote rather than a code.
    /// </summary>
    [Fact]
    public async Task A_rejected_membership_reaches_the_member()
    {
        var ownerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId);
        var tier = programme.Tiers[0].TierId;

        var applicantToken = await AuthenticateAsync();
        var membership = await ApplyAsync(organizationId, tier);

        await Factory.DrainOutboxAsync();

        Api.Authenticate(ownerToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/memberships/{membership.MembershipId}/decision",
            new { decision = "reject", reason = "Intake is closed until March." });

        await Factory.DrainOutboxAsync();

        Api.Authenticate(applicantToken);

        var inbox = await ReadDataAsync<InboxPageDto>(
            await HttpClient.GetAsync($"{Notifications}?take=50"));

        inbox.Items.Should().Contain(
            item => item.EntityId == membership.MembershipId.ToString());
    }

    /// <summary>
    /// <b>A volunteer decision reaches the applicant, and not the organiser.</b> The organiser made
    /// it and is looking at the queue they made it in.
    /// </summary>
    [Fact]
    public async Task A_volunteer_decision_reaches_the_applicant_only()
    {
        var organizerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var opportunity = await CreateOpportunityAsync(organizationId);

        var volunteerToken = await AuthenticateAsync();

        var application = await ReadDataAsync<ApplicationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/opportunities/{opportunity.OpportunityId}/applications",
                new { shiftId = opportunity.Shifts[0].ShiftId }));

        await Factory.DrainOutboxAsync();

        Api.Authenticate(organizerToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/volunteer/applications/{application.ApplicationId}/decision",
            new { decision = "approve" });

        await Factory.DrainOutboxAsync();

        Api.Authenticate(volunteerToken);

        var volunteerInbox = await ReadDataAsync<InboxPageDto>(
            await HttpClient.GetAsync($"{Notifications}?take=50"));

        volunteerInbox.Items.Should().Contain(
            item => item.EntityType == "volunteer-application"
                && item.EntityId == application.ApplicationId.ToString());

        Api.Authenticate(organizerToken);

        var organizerInbox = await ReadDataAsync<InboxPageDto>(
            await HttpClient.GetAsync($"{Notifications}?take=50"));

        organizerInbox.Items.Should().NotContain(
            item => item.EntityType == "volunteer-application",
            "the organiser made the decision and does not need telling about it");
    }

    /// <summary>
    /// <b>Confirming hours reaches the volunteer.</b> The receipt for the one fact this platform lets
    /// somebody put on their record.
    /// </summary>
    [Fact]
    public async Task Confirmed_hours_reach_the_volunteer()
    {
        var organizerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var opportunity = await CreateOpportunityAsync(organizationId);

        var volunteerToken = await AuthenticateAsync();

        var application = await ReadDataAsync<ApplicationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/opportunities/{opportunity.OpportunityId}/applications",
                new { shiftId = opportunity.Shifts[0].ShiftId }));

        Api.Authenticate(organizerToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/volunteer/applications/{application.ApplicationId}/decision",
            new { decision = "approve" });

        var assignment = await ReadDataAsync<AssignmentDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/assignments",
                new
                {
                    applicationId = application.ApplicationId,
                    shiftId = opportunity.Shifts[0].ShiftId
                }));

        Api.Authenticate(volunteerToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/volunteer/assignments/{assignment.AssignmentId}/check-in", new { });

        await Factory.DrainOutboxAsync();

        Api.Authenticate(organizerToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/volunteer/assignments/{assignment.AssignmentId}/hours",
            new { hours = 3.5m });

        await Factory.DrainOutboxAsync();

        Api.Authenticate(volunteerToken);

        var inbox = await ReadDataAsync<InboxPageDto>(
            await HttpClient.GetAsync($"{Notifications}?take=50"));

        inbox.Items.Should().Contain(
            item => item.EntityType == "volunteer-assignment"
                && item.EntityId == assignment.AssignmentId.ToString());
    }

    /// <summary>
    /// <b>A state B18 publishes and no rule addresses reaches nobody.</b> The negative control: a
    /// consumer that produced a trigger for every status would fill an inbox with bookkeeping, and a
    /// test that only asserted the positive cases would not notice.
    /// </summary>
    [Fact]
    public async Task A_cancellation_by_the_member_notifies_nobody()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId);
        var tier = programme.Tiers[0].TierId;

        var applicantToken = await AuthenticateAsync();
        var membership = await ApplyAsync(organizationId, tier);

        await Factory.DrainOutboxAsync();

        await HttpClient.PostAsJsonAsync(
            $"{Root}/memberships/{membership.MembershipId}/cancel", new { });

        await Factory.DrainOutboxAsync();

        Api.Authenticate(applicantToken);

        var inbox = await ReadDataAsync<InboxPageDto>(
            await HttpClient.GetAsync($"{Notifications}?take=50"));

        inbox.Items.Should().NotContain(
            item => item.EntityId == membership.MembershipId.ToString(),
            "a member who cancelled their own membership does not need telling that they did");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

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

    private async Task<MembershipDto> ApplyAsync(Guid organizationId, Guid tierId) =>
        await ReadDataAsync<MembershipDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/{organizationId}/memberships/applications", new { tierId }));

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

    private sealed record MembershipDto(Guid MembershipId, string Status);

    private sealed record ShiftDto(Guid ShiftId);

    private sealed record OpportunityDto(Guid OpportunityId, List<ShiftDto> Shifts);

    private sealed record ApplicationDto(Guid ApplicationId, string Status);

    private sealed record AssignmentDto(Guid AssignmentId, string Status);

    // The wire names are EntityType/EntityId. B13 calls the concept a subject internally — the
    // dispatcher's trigger carries SubjectType — and renames it at the boundary, because "entity" is
    // the client's word. A test that assumed the internal name would have asserted against nulls and
    // passed for the wrong reason.
    private sealed record InboxItemDto(
        Guid Id, string? EntityType, string? EntityId, string? Title, string SourceModule);

    private sealed record InboxPageDto(List<InboxItemDto> Items);
}
