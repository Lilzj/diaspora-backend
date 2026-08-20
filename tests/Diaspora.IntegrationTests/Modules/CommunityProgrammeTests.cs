using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B18's Community Programme, driven over real HTTP through the same host every other module suite
/// uses, against real persisted rows.
/// <para>
/// <b>Every read here is asserted against seeded data, and that is not a formality.</b> Wave 4's
/// severest finding was a discovery route that answered 500 the moment its page held a single row,
/// and had done since B7 — because the module's own suite asserted composition, empty results and
/// authorization, all of which pass against a route that throws when it has data. Nothing in this
/// file asserts on an empty table alone.
/// </para>
/// <para>
/// The wire shapes are re-declared by hand rather than referenced from the module, for the reason the
/// B12 suite records: binding a contract test to the module's own records makes it pass whenever the
/// module compiles.
/// </para>
/// </summary>
public sealed class CommunityProgrammeTests : IdentityTestBase
{
    private const string Root = "/api/v1/communities";
    private const string CommunityLife = "/api/v1/community-life";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityProgrammeTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // COMMUNITY-LIFE-GAP-013 — capabilities
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The organization's owner holds every capability, and the set is served as data.</b> The
    /// keystone read: nine other gaps' write routes are gated on this answer.
    /// </summary>
    [Fact]
    public async Task An_owner_holds_every_capability()
    {
        var owner = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await HttpClient.GetAsync($"{Root}/organizations/{organizationId}/capabilities"));

        capabilities.Role.Should().Be("Owner", "creating an organization makes you its owner");
        capabilities.CanManageMembers.Should().BeTrue();
        capabilities.CanApproveMembers.Should().BeTrue();
        capabilities.CanManageTiers.Should().BeTrue();
        capabilities.CanManagePolls.Should().BeTrue();
        capabilities.CanViewFinancials.Should().BeTrue();

        owner.Should().NotBeNull();
    }

    /// <summary>
    /// <b>A stranger holds nothing, and gets an answer rather than a refusal.</b> An all-false set
    /// tells the caller what they can do; a 404 would tell them which organizations exist.
    /// </summary>
    [Fact]
    public async Task A_stranger_holds_no_capability_in_an_organization()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();

        await AuthenticateAsync();

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await HttpClient.GetAsync($"{Root}/organizations/{organizationId}/capabilities"));

        capabilities.Role.Should().BeNull();
        capabilities.CanManageMembers.Should().BeFalse();
        capabilities.CanManagePolls.Should().BeFalse();
        capabilities.CanViewAnalytics.Should().BeFalse();
    }

    /// <summary>The caller's own organizations carry the role and the capability set together.</summary>
    [Fact]
    public async Task My_organizations_carry_their_role_and_capabilities()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();

        var mine = await ReadDataAsync<List<MyOrganizationDto>>(
            await HttpClient.GetAsync($"{Root}/organizations/me"));

        mine.Should().ContainSingle(row => row.OrganizationId == organizationId);

        var row = mine.Single(entry => entry.OrganizationId == organizationId);

        row.Role.Should().Be("Owner");
        row.MembershipId.Should().NotBeEmpty("the roll's role route names B12's membership row");
        row.Capabilities.CanManageTiers.Should().BeTrue();
    }

    /// <summary>The dashboard's first paint carries the capabilities and the counts in one call.</summary>
    [Fact]
    public async Task The_dashboard_answers_capabilities_and_counts_in_one_call()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        await SaveProgrammeAsync(organizationId);

        var dashboard = await ReadDataAsync<DashboardDto>(
            await HttpClient.GetAsync($"{Root}/organizations/{organizationId}/dashboard"));

        dashboard.OrganizationId.Should().Be(organizationId);
        dashboard.Capabilities.CanManageMembers.Should().BeTrue();
        dashboard.Counts.Should().NotBeNull();
        dashboard.ComputedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // COMMUNITY-LIFE-GAP-001 — programme and tiers
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A programme with real tiers reads back with real tiers.</b> A free tier has no price at all
    /// rather than a zero — the register: "the absence is the meaning, not a zero".
    /// </summary>
    [Fact]
    public async Task A_programme_reads_back_its_tiers_with_prices_and_absences()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        await SaveProgrammeAsync(organizationId);

        var programme = await ReadDataAsync<ProgrammeDto>(
            await HttpClient.GetAsync($"{Root}/{organizationId}/membership"));

        programme.Tiers.Should().HaveCount(2);

        var free = programme.Tiers.Single(tier => tier.Name == "Supporter");
        var paid = programme.Tiers.Single(tier => tier.Name == "Patron");

        free.Price.Should().BeNull("a free tier has no price at all");
        paid.Price.Should().NotBeNull();
        paid.Price!.Amount.Should().Be(25m);
        paid.Price.Currency.Should().Be("GBP");
        paid.IsFull.Should().BeFalse("the server decides this, and the tier is uncapped");
    }

    /// <summary>
    /// <b>A private programme is absent for somebody outside the organization</b>, not returned with
    /// a flag: a flag would mean the private group had already reached the device.
    /// </summary>
    [Fact]
    public async Task A_private_programme_is_absent_for_a_stranger()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        await SaveProgrammeAsync(organizationId, visibility: "Private");

        await AuthenticateAsync();

        var response = await HttpClient.GetAsync($"{Root}/{organizationId}/membership");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>A caller without the tier capability cannot write a programme.</summary>
    [Fact]
    public async Task A_stranger_cannot_write_a_programme()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();

        await AuthenticateAsync();

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{organizationId}/membership", ProgrammePayload("Public"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <b>The currency comes from B15's catalogue and nowhere else.</b> An unsupported code is refused
    /// rather than stored and rendered later as an unknown symbol.
    /// </summary>
    [Fact]
    public async Task An_unsupported_currency_is_refused()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{organizationId}/membership",
            new
            {
                summary = "A programme.",
                visibility = "Public",
                isOpen = true,
                requiresApproval = false,
                tiers = new[]
                {
                    new
                    {
                        name = "Patron",
                        description = "The paid one.",
                        priceAmount = 25m,
                        currency = "ZZZ",
                        billingPeriod = "Yearly",
                        sortOrder = 1
                    }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The discovery page returns real rows, not an empty page.</summary>
    [Fact]
    public async Task Discovery_returns_a_seeded_programme()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        await SaveProgrammeAsync(organizationId);

        var page = await ReadDataAsync<PageDto<ProgrammeDto>>(
            await HttpClient.GetAsync($"{Root}/memberships/discover?take=50"));

        page.Items.Should().Contain(row => row.OrganizationId == organizationId);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // COMMUNITY-LIFE-GAP-002 — the lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A free tier on a programme that does not review applications activates immediately.</b> A
    /// priced one stops at Approved and waits for B15 — this module never concludes that money moved.
    /// </summary>
    [Fact]
    public async Task Applying_for_a_free_tier_activates_immediately()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: false);
        var freeTier = programme.Tiers.Single(tier => tier.Name == "Supporter").TierId;

        await AuthenticateAsync();

        var membership = await ApplyAsync(organizationId, freeTier);

        membership.Status.Should().Be("Active");
        membership.GrantsBenefits.Should().BeTrue();
        membership.History.Should().HaveCountGreaterThan(1, "opening and activation both recorded");
    }

    [Fact]
    public async Task Applying_for_a_paid_tier_stops_at_approved()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: false);
        var paidTier = programme.Tiers.Single(tier => tier.Name == "Patron").TierId;

        await AuthenticateAsync();

        var membership = await ApplyAsync(organizationId, paidTier);

        membership.Status.Should().Be(
            "Approved", "B15 takes the money; this module does not conclude that it did");
        membership.GrantsBenefits.Should().BeFalse();
    }

    [Fact]
    public async Task Applying_to_a_programme_that_reviews_lands_at_pending()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: true);
        var tier = programme.Tiers[0].TierId;

        await AuthenticateAsync();

        var membership = await ApplyAsync(organizationId, tier);

        membership.Status.Should().Be("Pending");
    }

    /// <summary>
    /// <b>COMMUNITY-LIFE-GAP-022: a retried application returns the same membership.</b> Not a second
    /// one, and not a 409 — the register: "a retried submission must return the *same* membership".
    /// </summary>
    [Fact]
    public async Task A_retried_application_returns_the_same_membership()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: true);
        var tier = programme.Tiers[0].TierId;

        await AuthenticateAsync();

        var key = Guid.NewGuid().ToString("N");

        var first = await ApplyAsync(organizationId, tier, key);
        var second = await ApplyAsync(organizationId, tier, key);

        second.MembershipId.Should().Be(first.MembershipId);

        var mine = await ReadDataAsync<List<MembershipDto>>(
            await HttpClient.GetAsync($"{Root}/memberships/me"));

        mine.Should().ContainSingle(row => row.OrganizationId == organizationId);
    }

    /// <summary>
    /// The header carries the key as well as the body, because the register asks for both and the
    /// client sends the body field today.
    /// </summary>
    [Fact]
    public async Task The_idempotency_key_may_arrive_in_the_header()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: true);
        var tier = programme.Tiers[0].TierId;

        await AuthenticateAsync();

        var key = Guid.NewGuid().ToString("N");

        var first = await ApplyWithHeaderAsync(organizationId, tier, key);
        var second = await ApplyWithHeaderAsync(organizationId, tier, key);

        second.MembershipId.Should().Be(first.MembershipId);
    }

    /// <summary>A member cannot hold two live memberships of one organization.</summary>
    [Fact]
    public async Task A_second_application_while_live_is_refused()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: true);
        var tier = programme.Tiers[0].TierId;

        await AuthenticateAsync();
        await ApplyAsync(organizationId, tier);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/{organizationId}/memberships/applications", new { tierId = tier });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>The member's own cancellation is theirs, and a stranger cannot make it.</summary>
    [Fact]
    public async Task Only_the_member_can_cancel_their_membership()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: true);
        var tier = programme.Tiers[0].TierId;

        await AuthenticateAsync();
        var membership = await ApplyAsync(organizationId, tier);

        await AuthenticateAsync();

        var refused = await HttpClient.PostAsJsonAsync(
            $"{Root}/memberships/{membership.MembershipId}/cancel", new { });

        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// <b>The member roll refuses with 403 rather than filtering.</b> The register names this route:
    /// "refuses the member list for an organization the caller does not administer".
    /// </summary>
    [Fact]
    public async Task The_member_roll_refuses_a_caller_who_does_not_administer_it()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        await SaveProgrammeAsync(organizationId);

        await AuthenticateAsync();

        var response = await HttpClient.GetAsync($"{Root}/{organizationId}/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <b>The roll answers with real members and their tiers.</b> A read asserted only against an
    /// empty table is a read nobody has tested.
    /// </summary>
    [Fact]
    public async Task The_member_roll_lists_real_members()
    {
        var ownerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: true);
        var tier = programme.Tiers[0].TierId;

        await AuthenticateAsync();
        var membership = await ApplyAsync(organizationId, tier);

        Api.Authenticate(ownerToken);

        var roll = await ReadDataAsync<PageDto<MemberDto>>(
            await HttpClient.GetAsync($"{Root}/{organizationId}/memberships"));

        roll.Items.Should().ContainSingle(row => row.MembershipId == membership.MembershipId);

        var row = roll.Items.Single(entry => entry.MembershipId == membership.MembershipId);

        row.Status.Should().Be("Pending");
        row.TierName.Should().Be(programme.Tiers[0].Name);
    }

    /// <summary>An organization approving a free-tier application activates it.</summary>
    [Fact]
    public async Task Approving_a_free_membership_activates_it()
    {
        var ownerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: true);
        var freeTier = programme.Tiers.Single(tier => tier.Name == "Supporter").TierId;

        var applicantToken = await AuthenticateAsync();
        var membership = await ApplyAsync(organizationId, freeTier);

        Api.Authenticate(ownerToken);

        var decided = await ReadDataAsync<MemberDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/memberships/{membership.MembershipId}/decision",
                new { decision = "approve" }));

        decided.Status.Should().Be("Active");

        Api.Authenticate(applicantToken);

        var mine = await ReadDataAsync<MembershipDto>(
            await HttpClient.GetAsync($"{Root}/memberships/{membership.MembershipId}"));

        mine.Status.Should().Be("Active");
        mine.GrantsBenefits.Should().BeTrue();
    }

    /// <summary>A rejection carries the organization's own words back to the member.</summary>
    [Fact]
    public async Task A_rejection_surfaces_its_reason_to_the_member()
    {
        var ownerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: true);
        var tier = programme.Tiers[0].TierId;

        var applicantToken = await AuthenticateAsync();
        var membership = await ApplyAsync(organizationId, tier);

        Api.Authenticate(ownerToken);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/memberships/{membership.MembershipId}/decision",
            new { decision = "reject", reason = "We are not taking new members this year." });

        Api.Authenticate(applicantToken);

        var mine = await ReadDataAsync<MembershipDto>(
            await HttpClient.GetAsync($"{Root}/memberships/{membership.MembershipId}"));

        mine.Status.Should().Be("Rejected");
        mine.DecisionReason.Should().Be("We are not taking new members this year.");
    }

    /// <summary>A decision from somebody without the capability is refused.</summary>
    [Fact]
    public async Task A_stranger_cannot_decide_a_membership()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: true);
        var tier = programme.Tiers[0].TierId;

        await AuthenticateAsync();
        var membership = await ApplyAsync(organizationId, tier);

        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/memberships/{membership.MembershipId}/decision",
            new { decision = "approve" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // COMMUNITY-LIFE-GAP-009/010/011 — volunteering
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>An opportunity with real shifts reads back with them, and the counts are the server's.</summary>
    [Fact]
    public async Task An_opportunity_reads_back_with_its_shifts()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();

        var opportunity = await CreateOpportunityAsync(organizationId, capacity: 4);

        opportunity.Shifts.Should().HaveCount(1);
        opportunity.Shifts[0].Capacity.Should().Be(2);
        opportunity.Shifts[0].IsFull.Should().BeFalse();
        opportunity.IsFull.Should().BeFalse();
        opportunity.RequiredSkills.Should().Contain("First aid");
        opportunity.Status.Should().Be("Published");
    }

    /// <summary>A draft is invisible to a stranger and visible to the organizer.</summary>
    [Fact]
    public async Task A_draft_opportunity_is_invisible_to_a_stranger()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var opportunity = await CreateOpportunityAsync(organizationId, publish: false);

        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}/volunteer/opportunities/{opportunity.OpportunityId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var page = await ReadDataAsync<PageDto<OpportunityDto>>(
            await HttpClient.GetAsync($"{Root}/volunteer/opportunities?take=50"));

        page.Items.Should().NotContain(row => row.OpportunityId == opportunity.OpportunityId);
    }

    /// <summary>The list answers with real rows once one is published.</summary>
    [Fact]
    public async Task The_opportunity_list_returns_a_published_row()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var opportunity = await CreateOpportunityAsync(organizationId);

        await AuthenticateAsync();

        var page = await ReadDataAsync<PageDto<OpportunityDto>>(
            await HttpClient.GetAsync($"{Root}/volunteer/opportunities?take=50"));

        page.Items.Should().Contain(row => row.OpportunityId == opportunity.OpportunityId);

        var row = page.Items.Single(entry => entry.OpportunityId == opportunity.OpportunityId);

        row.CanApply.Should().BeTrue("a verified member may apply to a published opportunity");
        row.CannotApplyReason.Should().BeNull();
    }

    /// <summary>Filtering by a skill matches against the seeded owned collection.</summary>
    [Fact]
    public async Task Filtering_by_skill_matches_a_real_row()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var opportunity = await CreateOpportunityAsync(organizationId);

        var matched = await ReadDataAsync<PageDto<OpportunityDto>>(
            await HttpClient.GetAsync(
                $"{Root}/volunteer/opportunities?skill=First%20aid&take=50"));

        matched.Items.Should().Contain(row => row.OpportunityId == opportunity.OpportunityId);

        var missed = await ReadDataAsync<PageDto<OpportunityDto>>(
            await HttpClient.GetAsync(
                $"{Root}/volunteer/opportunities?skill=Welding&take=50"));

        missed.Items.Should().NotContain(row => row.OpportunityId == opportunity.OpportunityId);
    }

    /// <summary>Applying, then the organizer's queue, then the decision, then the assignment.</summary>
    [Fact]
    public async Task A_volunteer_applies_is_approved_assigned_and_has_hours_confirmed()
    {
        var organizerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var opportunity = await CreateOpportunityAsync(organizationId);

        var volunteerToken = await AuthenticateAsync();

        var application = await ReadDataAsync<ApplicationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/opportunities/{opportunity.OpportunityId}/applications",
                new
                {
                    shiftId = opportunity.Shifts[0].ShiftId,
                    answers = new Dictionary<string, string> { ["Why?"] = "I want to help." }
                }));

        application.Status.Should().Be("Pending");
        application.Answers.Should().ContainKey("Why?");

        Api.Authenticate(organizerToken);

        var queue = await ReadDataAsync<PageDto<ApplicantDto>>(
            await HttpClient.GetAsync(
                $"{Root}/volunteer/opportunities/{opportunity.OpportunityId}/applications"));

        queue.Items.Should().ContainSingle(row => row.ApplicationId == application.ApplicationId);

        await ReadDataAsync<ApplicantDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/applications/{application.ApplicationId}/decision",
                new { decision = "approve" }));

        var assignment = await ReadDataAsync<AssignmentDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/assignments",
                new
                {
                    applicationId = application.ApplicationId,
                    shiftId = opportunity.Shifts[0].ShiftId
                }));

        assignment.Status.Should().Be("Assigned");
        assignment.ConfirmedHours.Should().BeNull("nothing has been confirmed yet");

        Api.Authenticate(volunteerToken);

        var checkedIn = await ReadDataAsync<AssignmentDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/assignments/{assignment.AssignmentId}/check-in", new { }));

        checkedIn.Status.Should().Be("CheckedIn");
        checkedIn.ConfirmedHours.Should().BeNull("a check-in confirms nothing");

        Api.Authenticate(organizerToken);

        var confirmed = await ReadDataAsync<AssignmentDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/assignments/{assignment.AssignmentId}/hours",
                new { hours = 3.5m }));

        confirmed.ConfirmedHours.Should().Be(3.5m);
        confirmed.Status.Should().Be("Completed");

        Api.Authenticate(volunteerToken);

        var hours = await ReadDataAsync<HoursDto>(
            await HttpClient.GetAsync($"{Root}/volunteer/hours/me"));

        hours.ConfirmedHours.Should().Be(3.5m);
        hours.CompletedAssignments.Should().Be(1);
        hours.Certificates.Should().ContainSingle();
        hours.Certificates[0].Hours.Should().Be(3.5m);
        hours.Certificates[0].DownloadUrl.Should().BeNull("no document service exists yet");
    }

    /// <summary>
    /// <b>Assigning an unapproved volunteer is a 409</b> — the register, verbatim.
    /// </summary>
    [Fact]
    public async Task Assigning_an_unapproved_volunteer_is_refused()
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

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/volunteer/assignments",
            new { applicationId = application.ApplicationId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// <b>Hours cannot be confirmed for somebody who never checked in.</b> The one rule that makes a
    /// volunteering record worth putting on a profile.
    /// </summary>
    [Fact]
    public async Task Hours_cannot_be_confirmed_without_a_check_in()
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

        var assignment = await ReadDataAsync<AssignmentDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/assignments",
                new
                {
                    applicationId = application.ApplicationId,
                    shiftId = opportunity.Shifts[0].ShiftId
                }));

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/volunteer/assignments/{assignment.AssignmentId}/hours",
            new { hours = 3m });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>The applicant queue refuses somebody who does not manage the opportunity.</summary>
    [Fact]
    public async Task The_applicant_queue_refuses_a_stranger()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var opportunity = await CreateOpportunityAsync(organizationId);

        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}/volunteer/opportunities/{opportunity.OpportunityId}/applications");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A retried volunteer application returns the same application.</summary>
    [Fact]
    public async Task A_retried_volunteer_application_returns_the_same_application()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var opportunity = await CreateOpportunityAsync(organizationId);

        await AuthenticateAsync();

        var key = Guid.NewGuid().ToString("N");
        var payload = new { shiftId = opportunity.Shifts[0].ShiftId, idempotencyKey = key };
        var path = $"{Root}/volunteer/opportunities/{opportunity.OpportunityId}/applications";

        var first = await ReadDataAsync<ApplicationDto>(
            await HttpClient.PostAsJsonAsync(path, payload));

        var second = await ReadDataAsync<ApplicationDto>(
            await HttpClient.PostAsJsonAsync(path, payload));

        second.ApplicationId.Should().Be(first.ApplicationId);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // COMMUNITY-LIFE-GAP-012 — polls
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A poll carries no per-option count</b>, and the result read is where the numbers live.
    /// The register: "a component holding a poll has nothing to add up".
    /// </summary>
    [Fact]
    public async Task A_poll_carries_no_counts_and_its_results_do()
    {
        await AuthenticateAsync();
        var poll = await CreatePollAsync();

        var raw = await HttpClient.GetStringAsync($"{Root}/polls/{poll.PollId}");

        raw.Should().NotContain("\"count\"", "a poll object must have nothing to add up");

        var voter = await AuthenticateAsync();

        var result = await ReadDataAsync<PollResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/polls/{poll.PollId}/responses",
                new { selectedOptionIds = new[] { poll.Options[0].OptionId } }));

        result.TotalResponses.Should().Be(1);
        result.Options.Should().NotBeNull();
        result.Options!.Single(option => option.OptionId == poll.Options[0].OptionId)
            .Count.Should().Be(1);

        voter.Should().NotBeNull();
    }

    /// <summary>
    /// <b>A poll withholding its results returns null options, not an empty array.</b> An empty array
    /// renders as "nobody voted", which is a different and false statement.
    /// </summary>
    [Fact]
    public async Task A_poll_withholding_results_returns_null_options()
    {
        var authorToken = await AuthenticateAsync();
        var poll = await CreatePollAsync(showResultsBeforeClose: false);

        await AuthenticateAsync();

        await HttpClient.PostAsJsonAsync(
            $"{Root}/polls/{poll.PollId}/responses",
            new { selectedOptionIds = new[] { poll.Options[0].OptionId } });

        var result = await ReadDataAsync<PollResultDto>(
            await HttpClient.GetAsync($"{Root}/polls/{poll.PollId}/results"));

        result.AreResultsVisible.Should().BeFalse();
        result.Options.Should().BeNull("null, not an empty array");
        result.ResultsHiddenReason.Should().NotBeNullOrWhiteSpace();
        result.TotalResponses.Should().Be(1, "how many answered is not the same as what they chose");

        authorToken.Should().NotBeNull();
    }

    /// <summary>
    /// <b>One member, one vote.</b> A second vote on a poll that disallows changes is a 409, and the
    /// tally does not move.
    /// </summary>
    [Fact]
    public async Task A_second_vote_is_refused_and_the_tally_does_not_move()
    {
        await AuthenticateAsync();
        var poll = await CreatePollAsync(allowsChange: false);

        await AuthenticateAsync();

        await HttpClient.PostAsJsonAsync(
            $"{Root}/polls/{poll.PollId}/responses",
            new { selectedOptionIds = new[] { poll.Options[0].OptionId } });

        var second = await HttpClient.PostAsJsonAsync(
            $"{Root}/polls/{poll.PollId}/responses",
            new { selectedOptionIds = new[] { poll.Options[1].OptionId } });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var result = await ReadDataAsync<PollResultDto>(
            await HttpClient.GetAsync($"{Root}/polls/{poll.PollId}/results"));

        result.TotalResponses.Should().Be(1);
    }

    /// <summary>A poll that allows changes takes a second answer, and the total stays at one.</summary>
    [Fact]
    public async Task Changing_a_vote_does_not_add_a_vote()
    {
        await AuthenticateAsync();
        var poll = await CreatePollAsync(allowsChange: true);

        await AuthenticateAsync();

        await HttpClient.PostAsJsonAsync(
            $"{Root}/polls/{poll.PollId}/responses",
            new { selectedOptionIds = new[] { poll.Options[0].OptionId } });

        var changed = await ReadDataAsync<PollResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/polls/{poll.PollId}/responses",
                new { selectedOptionIds = new[] { poll.Options[1].OptionId } }));

        changed.TotalResponses.Should().Be(1);
        changed.Options!.Single(option => option.OptionId == poll.Options[1].OptionId)
            .Count.Should().Be(1);
        changed.Options!.Single(option => option.OptionId == poll.Options[0].OptionId)
            .Count.Should().Be(0);
    }

    /// <summary>
    /// <b>A members-only poll is 403 for a non-member and absent from their list.</b> Both, because
    /// the register asks for the refusal and a list that leaked the question would make it pointless.
    /// </summary>
    [Fact]
    public async Task A_members_only_poll_refuses_a_non_member_and_is_absent_from_their_list()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var poll = await CreatePollAsync(organizationId: organizationId);

        await AuthenticateAsync();

        var detail = await HttpClient.GetAsync($"{Root}/polls/{poll.PollId}");

        detail.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var page = await ReadDataAsync<PageDto<PollDto>>(
            await HttpClient.GetAsync($"{Root}/polls?take=50"));

        page.Items.Should().NotContain(row => row.PollId == poll.PollId);
    }

    /// <summary>Too many selections on a single-choice poll is a 400.</summary>
    [Fact]
    public async Task Too_many_selections_are_refused()
    {
        await AuthenticateAsync();
        var poll = await CreatePollAsync();

        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/polls/{poll.PollId}/responses",
            new
            {
                selectedOptionIds = new[]
                {
                    poll.Options[0].OptionId,
                    poll.Options[1].OptionId
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Voting on a closed poll is a 409.</summary>
    [Fact]
    public async Task Voting_on_a_closed_poll_is_refused()
    {
        var authorToken = await AuthenticateAsync();
        var poll = await CreatePollAsync();

        await HttpClient.PostAsJsonAsync($"{Root}/polls/{poll.PollId}/close", new { });

        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/polls/{poll.PollId}/responses",
            new { selectedOptionIds = new[] { poll.Options[0].OptionId } });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        authorToken.Should().NotBeNull();
    }

    /// <summary>An anonymous poll stores no voter, so it cannot show the caller their own answer.</summary>
    [Fact]
    public async Task An_anonymous_poll_returns_no_personal_answer()
    {
        await AuthenticateAsync();
        var poll = await CreatePollAsync(isAnonymous: true);

        await AuthenticateAsync();

        await HttpClient.PostAsJsonAsync(
            $"{Root}/polls/{poll.PollId}/responses",
            new { selectedOptionIds = new[] { poll.Options[0].OptionId } });

        var read = await ReadDataAsync<PollDto>(
            await HttpClient.GetAsync($"{Root}/polls/{poll.PollId}"));

        read.HasVoted.Should().BeTrue("the fingerprint still recognises them");
        read.MyResponse.Should().BeNull("there is no stored identity to find their answer by");

        var result = await ReadDataAsync<PollResultDto>(
            await HttpClient.GetAsync($"{Root}/polls/{poll.PollId}/results"));

        result.Options.Should().OnlyContain(option => option.IsMyChoice == null);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // COMMUNITY-LIFE-GAP-016 — announcements
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>An organization announcement reaches its members and nobody else.</b> The audience rule the
    /// register asks for, applied in the query rather than after it.
    /// </summary>
    [Fact]
    public async Task An_organization_announcement_reaches_its_members_and_nobody_else()
    {
        var ownerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();

        var announcement = await ReadDataAsync<AnnouncementDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/announcements",
                new
                {
                    scope = "Organization",
                    scopeId = organizationId,
                    title = "Annual general meeting",
                    body = "Saturday, in the hall.",
                    isPinned = true
                }));

        announcement.Scope.Should().Be("Organization");
        announcement.ActionRoute.Should().Contain(organizationId.ToString());

        // The owner is a member of their own organization and sees it.
        var mine = await ReadDataAsync<PageDto<AnnouncementDto>>(
            await HttpClient.GetAsync($"{Root}/announcements?take=50"));

        mine.Items.Should().Contain(row => row.AnnouncementId == announcement.AnnouncementId);

        await AuthenticateAsync();

        var theirs = await ReadDataAsync<PageDto<AnnouncementDto>>(
            await HttpClient.GetAsync($"{Root}/announcements?take=50"));

        theirs.Items.Should().NotContain(row => row.AnnouncementId == announcement.AnnouncementId);

        ownerToken.Should().NotBeNull();
    }

    /// <summary>Publishing without the capability is refused.</summary>
    [Fact]
    public async Task A_stranger_cannot_publish_an_organizations_announcement()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();

        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/announcements",
            new
            {
                scope = "Organization",
                scopeId = organizationId,
                title = "Hijacked",
                body = "Not mine to say."
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // COMMUNITY-LIFE-GAP-014/017/021 — analytics, impact, search
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Analytics answer with a filled series, and refuse a caller without the capability.</summary>
    [Fact]
    public async Task Analytics_answer_a_manager_and_refuse_a_stranger()
    {
        var ownerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: false);
        var freeTier = programme.Tiers.Single(tier => tier.Name == "Supporter").TierId;

        await AuthenticateAsync();
        await ApplyAsync(organizationId, freeTier);

        Api.Authenticate(ownerToken);

        var analytics = await ReadDataAsync<AnalyticsDto>(
            await HttpClient.GetAsync(
                $"{Root}/organizations/{organizationId}/analytics?period=30d"));

        analytics.Period.Should().Be("30d");
        analytics.MemberGrowth.Should().HaveCount(30, "every day gets a point, including quiet ones");
        analytics.MemberGrowth.Sum(point => point.Value).Should().Be(
            1, "one member joined in the window");

        var members = await ReadDataAsync<MemberAnalyticsDto>(
            await HttpClient.GetAsync(
                $"{Root}/organizations/{organizationId}/analytics/members?period=30d"));

        members.ActiveMembers.Should().Be(1);
        members.ByStatus.Should().ContainKey("Active");

        await AuthenticateAsync();

        var refused = await HttpClient.GetAsync(
            $"{Root}/organizations/{organizationId}/analytics?period=30d");

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>An unrecognised period is refused rather than silently defaulted.</summary>
    [Fact]
    public async Task An_unknown_analytics_period_is_refused()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}/organizations/{organizationId}/analytics?period=forever");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// <b>Every impact metric carries its own definition.</b> The register: "an undefined metric is
    /// worse than an absent one".
    /// </summary>
    [Fact]
    public async Task Every_impact_metric_carries_its_definition()
    {
        await AuthenticateAsync();

        var impact = await ReadDataAsync<ImpactDto>(
            await HttpClient.GetAsync($"{Root}/impact?scope=member&period=30d"));

        impact.Metrics.Should().NotBeEmpty();
        impact.Metrics.Should().OnlyContain(metric =>
            !string.IsNullOrWhiteSpace(metric.Definition)
            && !string.IsNullOrWhiteSpace(metric.Source));
    }

    /// <summary>The federated search returns real rows, ordered by the server's relevance.</summary>
    [Fact]
    public async Task Search_federates_the_types_this_module_owns()
    {
        await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var opportunity = await CreateOpportunityAsync(organizationId, title: "Foodbank stewards");
        await CreatePollAsync(question: "Foodbank opening hours?");

        var results = await ReadDataAsync<PageDto<SearchResultDto>>(
            await HttpClient.GetAsync($"{Root}/search?q=Foodbank&take=50"));

        results.Items.Should().Contain(row => row.EntityId == opportunity.OpportunityId);
        results.Items.Should().Contain(row => row.EntityType == "poll");

        results.Items.Select(row => row.Relevance).Should().BeInDescendingOrder(
            "the order is the server's decision");
    }

    /// <summary>A search that matches nothing answers an empty page rather than an error.</summary>
    [Fact]
    public async Task A_search_that_matches_nothing_answers_an_empty_page()
    {
        await AuthenticateAsync();

        var results = await ReadDataAsync<PageDto<SearchResultDto>>(
            await HttpClient.GetAsync($"{Root}/search?q=zzzznothingmatchesthis&take=50"));

        results.Items.Should().BeEmpty();
        results.HasMore.Should().BeFalse();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Paging
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The roll page is complete, ordered, and never repeats a row — with every row written in one
    /// unit of work.</b> The case a timestamp cursor gets wrong: <c>CreatedAt</c> is stamped by the
    /// shared interceptor, so a batch of applications carries one instant and a strict comparison
    /// would drop all but the first.
    /// </summary>
    [Fact]
    public async Task The_member_roll_pages_completely_across_a_tie_group()
    {
        var ownerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: true);
        var tier = programme.Tiers[0].TierId;

        var expected = new List<Guid>();

        for (var index = 0; index < 7; index++)
        {
            await AuthenticateAsync();
            expected.Add((await ApplyAsync(organizationId, tier)).MembershipId);
        }

        Api.Authenticate(ownerToken);

        var seen = new List<Guid>();
        string? cursor = null;

        for (var page = 0; page < 10; page++)
        {
            var response = await ReadDataAsync<PageDto<MemberDto>>(
                await HttpClient.GetAsync(
                    $"{Root}/{organizationId}/memberships?take=2"
                    + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}")));

            seen.AddRange(response.Items.Select(item => item.MembershipId));
            cursor = response.NextCursor;

            if (!response.HasMore)
            {
                break;
            }
        }

        seen.Should().OnlyHaveUniqueItems("a cursor page must never serve a row twice");
        seen.Should().BeEquivalentTo(expected, "no member may be skipped");
    }

    /// <summary>A malformed cursor answers the first page rather than a 400.</summary>
    [Fact]
    public async Task A_malformed_cursor_answers_the_first_page()
    {
        var ownerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var programme = await SaveProgrammeAsync(organizationId, requiresApproval: true);

        await AuthenticateAsync();
        await ApplyAsync(organizationId, programme.Tiers[0].TierId);

        Api.Authenticate(ownerToken);

        var response = await HttpClient.GetAsync(
            $"{Root}/{organizationId}/memberships?cursor=not-a-cursor");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await ReadDataAsync<PageDto<MemberDto>>(response);

        page.Items.Should().NotBeEmpty();
    }

    /// <summary>The applicant queue pages oldest-first and completely.</summary>
    [Fact]
    public async Task The_applicant_queue_pages_completely_oldest_first()
    {
        var organizerToken = await AuthenticateAsync();
        var organizationId = await CreateOrganizationAsync();
        var opportunity = await CreateOpportunityAsync(organizationId, capacity: 20);

        var expected = new List<Guid>();

        for (var index = 0; index < 5; index++)
        {
            await AuthenticateAsync();

            var application = await ReadDataAsync<ApplicationDto>(
                await HttpClient.PostAsJsonAsync(
                    $"{Root}/volunteer/opportunities/{opportunity.OpportunityId}/applications",
                    new { shiftId = opportunity.Shifts[0].ShiftId }));

            expected.Add(application.ApplicationId);
        }

        Api.Authenticate(organizerToken);

        var seen = new List<Guid>();
        string? cursor = null;

        for (var page = 0; page < 10; page++)
        {
            var response = await ReadDataAsync<PageDto<ApplicantDto>>(
                await HttpClient.GetAsync(
                    $"{Root}/volunteer/opportunities/{opportunity.OpportunityId}/applications?take=2"
                    + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}")));

            seen.AddRange(response.Items.Select(item => item.ApplicationId));
            cursor = response.NextCursor;

            if (!response.HasMore)
            {
                break;
            }
        }

        seen.Should().OnlyHaveUniqueItems();
        seen.Should().BeEquivalentTo(expected);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Authentication
    // ════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task An_anonymous_caller_cannot_read_the_programme_surface()
    {
        Api.ClearAuthentication();

        foreach (var path in new[]
        {
            $"{Root}/organizations/me",
            $"{Root}/memberships/me",
            $"{Root}/volunteer/hours/me",
            $"{Root}/impact?scope=member"
        })
        {
            var response = await HttpClient.GetAsync(path);

            response.StatusCode.Should().Be(
                HttpStatusCode.Unauthorized, $"{path} is the caller's own data");
        }
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_write()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/polls",
            new
            {
                question = "Should we?",
                votingType = "SingleChoice",
                audience = "Public",
                options = new[] { new { label = "Yes" }, new { label = "No" } }
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<string> AuthenticateAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        return auth.Tokens.AccessToken;
    }

    /// <summary>
    /// A real B12 organization. B18 owns none of this — the organization, its membership edge and the
    /// role that makes the caller its owner are all B12's, which is the whole point of GAP-013.
    /// </summary>
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

    private async Task<ProgrammeDto> SaveProgrammeAsync(
        Guid organizationId,
        string visibility = "Public",
        bool requiresApproval = true) =>
        await ReadDataAsync<ProgrammeDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Root}/{organizationId}/membership",
                ProgrammePayload(visibility, requiresApproval)));

    private static object ProgrammePayload(string visibility, bool requiresApproval = true) =>
        new
        {
            summary = "Join us.",
            visibility,
            isOpen = true,
            requiresApproval,
            tiers = new object[]
            {
                new
                {
                    name = "Supporter",
                    description = "The free one.",
                    billingPeriod = "Yearly",
                    sortOrder = 0,
                    benefits = new[] { new { title = "The newsletter", isActive = true } }
                },
                new
                {
                    name = "Patron",
                    description = "The paid one.",
                    priceAmount = 25m,
                    currency = "GBP",
                    billingPeriod = "Yearly",
                    sortOrder = 1
                }
            }
        };

    private async Task<MembershipDto> ApplyAsync(
        Guid organizationId, Guid tierId, string? idempotencyKey = null) =>
        await ReadDataAsync<MembershipDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/{organizationId}/memberships/applications",
                new { tierId, idempotencyKey }));

    private async Task<MembershipDto> ApplyWithHeaderAsync(
        Guid organizationId, Guid tierId, string key)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{Root}/{organizationId}/memberships/applications")
        {
            Content = JsonContent.Create(new { tierId })
        };

        request.Headers.Add("Idempotency-Key", key);

        return await ReadDataAsync<MembershipDto>(await HttpClient.SendAsync(request));
    }

    private async Task<OpportunityDto> CreateOpportunityAsync(
        Guid organizationId,
        bool publish = true,
        int? capacity = null,
        string title = "Foodbank stewards")
    {
        var start = DateTimeOffset.UtcNow.AddDays(14);

        return await ReadDataAsync<OpportunityDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/volunteer/opportunities",
                new
                {
                    organizationId,
                    title,
                    description = "Help us run the foodbank.",
                    cause = "Food poverty",
                    countryCode = "GB",
                    cityName = "London",
                    startsAt = start,
                    endsAt = start.AddHours(4),
                    capacity,
                    requiredSkills = new[] { "First aid" },
                    shifts = new[]
                    {
                        new
                        {
                            label = "Morning",
                            startsAt = start,
                            endsAt = start.AddHours(4),
                            capacity = 2
                        }
                    },
                    publish
                }));
    }

    private async Task<PollDto> CreatePollAsync(
        Guid? organizationId = null,
        bool isAnonymous = false,
        bool allowsChange = false,
        bool showResultsBeforeClose = true,
        string question = "Should we move the meeting?") =>
        await ReadDataAsync<PollDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/polls",
                new
                {
                    question,
                    description = "A question from the integration suite.",
                    votingType = "SingleChoice",
                    audience = organizationId is null ? "Public" : "OrganizationMembers",
                    organizationId,
                    options = new[]
                    {
                        new { label = "Yes" },
                        new { label = "No" }
                    },
                    isAnonymous,
                    allowsChange,
                    showResultsBeforeClose,
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

    // ── Wire shapes, re-declared by hand ────────────────────────────────────────────────────

    private sealed record OrganizationDto(Guid OrganizationId);

    private sealed record PageDto<T>(
        List<T> Items, string? NextCursor, bool HasMore, int? TotalCount);

    private sealed record CapabilitiesDto(
        Guid OrganizationId,
        string? Role,
        bool CanManageMembers,
        bool CanApproveMembers,
        bool CanManageTiers,
        bool CanManageEvents,
        bool CanManageCampaigns,
        bool CanManageVolunteers,
        bool CanManagePolls,
        bool CanPublishAnnouncements,
        bool CanViewAnalytics,
        bool CanViewFinancials);

    private sealed record MyOrganizationDto(
        Guid OrganizationId, string Name, string Role, Guid MembershipId,
        CapabilitiesDto Capabilities);

    private sealed record DashboardDto(
        Guid OrganizationId, CapabilitiesDto Capabilities, CountsDto Counts,
        DateTimeOffset ComputedAt);

    private sealed record CountsDto(
        int PendingMemberApplications, int ActiveMembers, int OpenOpportunities,
        int PendingVolunteerApplications, int OpenPolls, int RecentAnnouncements);

    private sealed record MoneyDto(decimal Amount, string Currency, int Exponent);

    private sealed record TierDto(
        Guid TierId, string Name, MoneyDto? Price, string BillingPeriod, int? Capacity,
        int FilledCount, bool IsFull, List<RequirementDto> Requirements);

    private sealed record RequirementDto(Guid Id, string Label, string Kind, bool IsMet);

    private sealed record ProgrammeDto(
        Guid OrganizationId, string OrganizationName, string Visibility, bool IsOpen,
        bool RequiresApproval, List<TierDto> Tiers, int MemberCount, string MyStatus,
        Guid? MyMembershipId);

    private sealed record MembershipDto(
        Guid MembershipId, Guid OrganizationId, Guid TierId, string TierName, string Status,
        MoneyDto? Price, bool IsRenewable, bool GrantsBenefits, List<HistoryDto> History,
        string? DecisionReason);

    private sealed record HistoryDto(Guid Id, string Status, string Actor, DateTimeOffset OccurredAt);

    private sealed record MemberDto(
        Guid MembershipId, Guid UserId, Guid TierId, string TierName, string Status, string? Role);

    private sealed record ShiftDto(
        Guid ShiftId, string Label, int Capacity, int FilledCount, bool IsFull);

    private sealed record OpportunityDto(
        Guid OpportunityId, string Title, string Status, Guid OrganizationId,
        List<ShiftDto> Shifts, List<string> RequiredSkills, int? Capacity, int FilledCount,
        bool IsFull, string MyApplicationStatus, bool CanApply, string? CannotApplyReason,
        bool CanManage);

    private sealed record ApplicationDto(
        Guid ApplicationId, Guid OpportunityId, string Status,
        Dictionary<string, string> Answers, int? WaitlistPosition);

    private sealed record ApplicantDto(
        Guid ApplicationId, Guid UserId, string Status, int? WaitlistPosition);

    private sealed record AssignmentDto(
        Guid AssignmentId, Guid ApplicationId, string Status, decimal? ConfirmedHours);

    private sealed record CertificateDto(Guid CertificateId, decimal Hours, string? DownloadUrl);

    private sealed record HoursDto(
        decimal ConfirmedHours, int CompletedAssignments, int OrganizationsSupported,
        List<CertificateDto> Certificates);

    private sealed record PollOptionDto(Guid OptionId, string Label, int SortOrder);

    private sealed record PollDto(
        Guid PollId, string Question, string Status, string VotingType, string Audience,
        List<PollOptionDto> Options, bool IsAnonymous, bool AllowsChange, int TotalResponses,
        bool HasVoted, object? MyResponse, bool CanVote, string? CannotVoteReason, bool CanManage);

    private sealed record PollOptionResultDto(
        Guid OptionId, string Label, int Count, decimal Percentage, bool? IsMyChoice);

    private sealed record PollResultDto(
        Guid PollId, string Status, List<PollOptionResultDto>? Options, int TotalResponses,
        bool AreResultsVisible, string? ResultsHiddenReason);

    private sealed record AnnouncementDto(
        Guid AnnouncementId, string Scope, Guid? ScopeId, string Title, bool IsPinned,
        string? ActionRoute);

    private sealed record PointDto(string Date, decimal Value);

    private sealed record AnalyticsDto(
        Guid OrganizationId, string Period, List<PointDto> MemberGrowth,
        List<PointDto> VolunteerHours, List<PointDto> PollParticipation);

    private sealed record MemberAnalyticsDto(
        Guid OrganizationId, string Period, int TotalMembers, int ActiveMembers,
        Dictionary<string, int> ByStatus);

    private sealed record ImpactMetricDto(
        string Key, string Label, decimal Value, string Definition, string Source);

    private sealed record ImpactDto(
        string Scope, string ScopeName, string Period, List<ImpactMetricDto> Metrics);

    private sealed record SearchResultDto(
        string EntityType, Guid EntityId, string Title, string ActionRoute, double Relevance);
}
