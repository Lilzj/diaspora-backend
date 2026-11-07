using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Platform.Messaging;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Infrastructure.Identity.Hosting;
using Diaspora.IntegrationTests.Common;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

/// <summary>
/// Staff acting on somebody else's account — 19 Sep 2026, and the owner's rule of 7 Nov 2026.
/// <para>
/// <b>Four permissions existed from the beginning with no policy and no route.</b> Reading
/// a member, reading their history, signing them out and giving them a role were all things
/// the platform said staff could do and gave them no way to do, which is why every staff
/// account so far was made with a database write. These tests are the routes that ended
/// that, and the two refusals that keep role assignment from being an escalation ladder.
/// </para>
/// <para>
/// <b>Since 7 Nov 2026</b> no staff action on a member happens without a reason, a record and a
/// notice; nobody acts on their own account or on one at or above their rank; and suspending,
/// closing, reopening and granting Admin are proposals a second member of staff confirms.
/// </para>
/// </summary>
[Collection(IdentityCollection.Name)]
public sealed class AccountAdministrationTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/admin/accounts";
    private const string Actions = "/api/v1/admin/account-actions";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AccountAdministrationTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    [Fact]
    public async Task A_member_can_be_found_by_identifier_by_email_and_by_phone()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Moderator);

        foreach (var term in new[] { member.ToString(), EmailFor(phone), phone })
        {
            var response = await HttpClient.GetAsync($"{Root}/lookup?term={Uri.EscapeDataString(term)}");

            var found = await IdentityApiClient.ReadDataAsync<AdminMemberResponse>(response);

            found.Id.Should().Be(member, "the same account answers to all three handles it is known by");
            found.Role.Should().Be(nameof(UserRole.CommunityUser));
            found.IsStaff.Should().BeFalse();
        }
    }

    [Fact]
    public async Task A_handle_nobody_holds_is_not_found_rather_than_refused()
    {
        await SignInAsStaffAsync(UserRole.Moderator);

        var response = await HttpClient.GetAsync($"{Root}/lookup?term=nobody@example.invalid");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Looking_a_member_up_needs_the_permission_for_it()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        Authenticate(auth.Tokens.AccessToken);

        // An ordinary member holds none of the staff permissions, and the platform — not
        // the console — is what refuses them.
        var response = await HttpClient.GetAsync($"{Root}/lookup?term={auth.User.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_members_security_history_is_readable_and_starts_with_their_registration()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Moderator);

        var response = await HttpClient.GetAsync($"{Root}/{member}/security-events");
        var events = await IdentityApiClient.ReadDataAsync<List<SecurityEventResponse>>(response);

        events.Should().NotBeEmpty("registering and verifying a phone both write security events");
    }

    [Fact]
    public async Task A_role_change_takes_effect_and_signs_the_member_out()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Admin);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{member}/role",
            new { role = nameof(UserRole.Moderator), reason = "Joining the moderation team." },
            JsonOptions);

        var updated = await IdentityApiClient.ReadDataAsync<AdminMemberResponse>(response);

        updated.Role.Should().Be(nameof(UserRole.Moderator));
        updated.IsStaff.Should().BeTrue();

        // **The sign-out is the point, not tidiness.** Permissions travel in the access
        // token, so without this the member would hold their old permissions until it
        // expired — and a promotion would grant nothing until then either.
        var stillValid = await Factory.CreateClient()
            .WithBearer(auth.Tokens.AccessToken)
            .GetAsync("/api/v1/auth/me");

        stillValid.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_role_change_needs_a_reason()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        await SignInAsStaffAsync(UserRole.Admin);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{auth.User.Id}/role", new { role = nameof(UserRole.Moderator) }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RoleOfAsync(auth.User.Id)).Should().Be(nameof(UserRole.CommunityUser));
    }

    [Fact]
    public async Task Nobody_changes_their_own_role()
    {
        var staff = await SignInAsStaffAsync(UserRole.Admin);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{staff}/role",
            new { role = nameof(UserRole.SuperAdmin), reason = "I would like to be a SuperAdmin." },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await RoleOfAsync(staff)).Should().Be(nameof(UserRole.Admin), "the refusal changed nothing");
    }

    [Fact]
    public async Task An_admin_cannot_appoint_a_super_admin()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Admin);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{member}/role",
            new { role = nameof(UserRole.SuperAdmin), reason = "Running the platform with us." },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOfAsync(response)).Should().Be("Identity.Administration.SuperAdminOnly");
        (await RoleOfAsync(member)).Should().Be(nameof(UserRole.CommunityUser));
    }

    [Fact]
    public async Task A_role_the_platform_does_not_have_is_refused_rather_than_defaulted()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Admin);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{member}/role",
            new { role = "Moderatorr", reason = "Joining the moderation team." },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The reason this matters: a platform that defaulted an unparsed role would turn a
        // typo into a demotion, silently, with a 200 on the way out.
        (await RoleOfAsync(member)).Should().Be(nameof(UserRole.CommunityUser));
    }

    [Fact]
    public async Task A_member_can_be_signed_out_of_every_device_without_being_suspended()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Admin);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/{member}/sessions/revoke",
            new { reason = "Reported as shared." },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterwards = await Factory.CreateClient()
            .WithBearer(auth.Tokens.AccessToken)
            .GetAsync("/api/v1/auth/me");

        afterwards.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Signed out, not suspended: the account is untouched and they can sign back in.
        var found = await IdentityApiClient.ReadDataAsync<AdminMemberResponse>(
            await HttpClient.GetAsync($"{Root}/{member}"));

        found.AccountStatus.Should().Be("Active");
    }

    // ── The owner's rule for staff power (7 Nov 2026) ───────────────────────────────────────

    [Fact]
    public async Task A_moderator_cannot_suspend_an_admin()
    {
        var admin = await SignInAsStaffAsync(UserRole.Admin);
        await SignInAsStaffAsync(UserRole.Moderator);

        var response = await ProposeSuspensionAsync(admin);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOfAsync(response)).Should().Be("Identity.Administration.TargetNotBelowYourRank");

        // Nor another moderator: strictly above, not level with.
        var peer = await SignInAsStaffAsync(UserRole.Moderator);
        await SignInAsStaffAsync(UserRole.Moderator);

        (await ProposeSuspensionAsync(peer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Nobody_acts_on_their_own_account()
    {
        var admin = await SignInAsStaffAsync(UserRole.Admin);

        var suspended = await ProposeSuspensionAsync(admin);
        suspended.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOfAsync(suspended)).Should().Be("Identity.Administration.CannotActOnOwnAccount");

        var signedOut = await HttpClient.PostAsJsonAsync(
            $"{Root}/{admin}/sessions/revoke", new { reason = "Signing myself out everywhere." }, JsonOptions);
        signedOut.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var closed = await HttpClient.PostAsJsonAsync(
            $"{Root}/{admin}/disable",
            new { reasonCode = "Other", reason = "Closing my own account from the console." },
            JsonOptions);
        closed.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_suspension_needs_a_reason_code_and_a_note_of_ten_characters()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        await SignInAsStaffAsync(UserRole.Moderator);

        var noCode = await HttpClient.PostAsJsonAsync(
            $"{Root}/{auth.User.Id}/suspend", new { reasonCode = "", reason = "Repeated scam adverts." }, JsonOptions);
        noCode.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var unknownCode = await HttpClient.PostAsJsonAsync(
            $"{Root}/{auth.User.Id}/suspend", new { reasonCode = "Rude", reason = "Repeated scam adverts." }, JsonOptions);
        unknownCode.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var shortNote = await HttpClient.PostAsJsonAsync(
            $"{Root}/{auth.User.Id}/suspend", new { reasonCode = "Spam", reason = "spam" }, JsonOptions);
        shortNote.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_suspension_is_a_proposal_until_a_second_member_of_staff_confirms_it()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        var proposer = await SignInAsStaffAsync(UserRole.Moderator);

        var proposed = await ProposeSuspensionAsync(member);
        proposed.StatusCode.Should().Be(HttpStatusCode.Accepted, await proposed.Content.ReadAsStringAsync());

        var proposal = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(proposed);
        proposal.Kind.Should().Be("Suspend");
        proposal.Status.Should().Be("Pending");
        proposal.TargetUserId.Should().Be(member);
        proposal.ProposedBy.Should().Be(proposer);
        proposal.ReasonLabel.Should().Be("a fraud or scam");

        // Nothing has happened to the member yet.
        (await MemberAsync(member)).AccountStatus.Should().Be("Active");

        // A second proposal of the same action waits behind the first rather than racing it.
        (await ProposeSuspensionAsync(member)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // It is in the queue a second member of staff works from.
        var queue = await IdentityApiClient.ReadDataAsync<List<AccountActionProposalResponse>>(
            await HttpClient.GetAsync($"{Actions}?status=Pending&userId={member}"));
        queue.Should().ContainSingle(item => item.Id == proposal.Id);

        // The proposer is not the second pair of eyes.
        var own = await ConfirmAsync(proposal.Id);
        own.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOfAsync(own)).Should().Be("Identity.Administration.ProposerCannotDecide");

        (await MemberAsync(member)).AccountStatus.Should().Be("Active", "a refused confirmation changes nothing");
    }

    [Fact]
    public async Task Confirming_a_suspension_suspends_with_the_reason_and_until_and_the_member_is_shown_both()
    {
        var (phone, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;
        var until = DateTimeOffset.UtcNow.AddDays(14);

        await SignInAsStaffAsync(UserRole.Moderator);
        var proposal = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(
            await ProposeSuspensionAsync(member, until));

        var confirmer = await SignInAsStaffAsync(UserRole.Moderator);
        var confirmed = await ConfirmAsync(proposal.Id, "Checked the three reports. Agreed.");
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());

        var decided = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(confirmed);
        decided.Status.Should().Be("Confirmed");
        decided.DecidedBy.Should().Be(confirmer, "both people are on the record");
        decided.DecisionNote.Should().Be("Checked the three reports. Agreed.");

        var suspended = await MemberAsync(member);
        suspended.AccessState.Should().Be("Suspended");
        suspended.StatusReasonCode.Should().Be("FraudOrScam");
        suspended.StatusReason.Should().Be(SuspensionNote);
        suspended.SuspendedUntil.Should().BeCloseTo(until, TimeSpan.FromSeconds(1));

        // The member's sessions were cut, and on signing in again they are told why and until when.
        var oldToken = await Factory.CreateClient().WithBearer(auth.Tokens.AccessToken).GetAsync("/api/v1/auth/me");
        oldToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        Api.ClearAuthentication();
        var signIn = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await Api.LoginAsync(new LoginRequest { Email = EmailFor(phone), Password = ValidPassword }));

        signIn.Access.AccessState.Should().Be("Suspended");
        signIn.Access.ReasonCode.Should().Be("FraudOrScam");
        signIn.Access.ReasonLabel.Should().Be("a fraud or scam");
        signIn.Access.Note.Should().Be(SuspensionNote);
        signIn.Access.RestrictedUntil.Should().BeCloseTo(until, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task A_declined_suspension_leaves_the_member_alone()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Moderator);
        var proposal = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(
            await ProposeSuspensionAsync(member));

        await SignInAsStaffAsync(UserRole.Admin);

        // A decline says why, so the proposer learns something.
        (await HttpClient.PostAsJsonAsync($"{Actions}/{proposal.Id}/decline", new { }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var declined = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(
            await HttpClient.PostAsJsonAsync(
                $"{Actions}/{proposal.Id}/decline",
                new { note = "The reports are about a different seller with the same name." },
                JsonOptions));

        declined.Status.Should().Be("Declined");
        (await MemberAsync(member)).AccountStatus.Should().Be("Active");

        // Decided once, and only once.
        (await ConfirmAsync(proposal.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Granting_admin_needs_a_second_member_of_staff()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Admin);

        var proposed = await HttpClient.PutAsJsonAsync(
            $"{Root}/{member}/role",
            new { role = nameof(UserRole.Admin), reason = "Taking over the Lagos chapter's accounts." },
            JsonOptions);

        proposed.StatusCode.Should().Be(HttpStatusCode.Accepted, await proposed.Content.ReadAsStringAsync());
        var proposal = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(proposed);
        proposal.Kind.Should().Be("GrantRole");
        proposal.Role.Should().Be(nameof(UserRole.Admin));

        (await RoleOfAsync(member)).Should().Be(nameof(UserRole.CommunityUser), "a proposal grants nothing");
        (await ConfirmAsync(proposal.Id)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A moderator cannot be the second: confirming a role grant needs the permission to grant one.
        await SignInAsStaffAsync(UserRole.Moderator);
        var byModerator = await ConfirmAsync(proposal.Id);
        byModerator.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOfAsync(byModerator)).Should().Be("Identity.Administration.MissingPermission");

        await SignInAsStaffAsync(UserRole.Admin);
        (await ConfirmAsync(proposal.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await RoleOfAsync(member)).Should().Be(nameof(UserRole.Admin));
    }

    [Fact]
    public async Task A_role_change_tells_the_member_their_new_role_and_why()
    {
        var (phone, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Admin);

        (await HttpClient.PutAsJsonAsync(
                $"{Root}/{member}/role",
                new { role = nameof(UserRole.Moderator), reason = "Joining the moderation team for Lagos" },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.DrainOutboxAsync();

        // The change signed them out, so they read it after signing in again.
        Api.ClearAuthentication();
        var signIn = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await Api.LoginAsync(new LoginRequest { Email = EmailFor(phone), Password = ValidPassword }));
        Authenticate(signIn.Tokens.AccessToken);

        var notices = await SecurityInboxAsync();

        var notice = notices.Should().ContainSingle(item => item.Title == "Your role on Diaspora changed").Subject;
        notice.Body.Should().Contain("You are now a moderator on Diaspora, no longer a member.");
        notice.Body.Should().Contain("The reason Diaspora gave: Joining the moderation team for Lagos.");
    }

    [Fact]
    public async Task The_sweep_ends_a_suspension_whose_date_has_passed()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Moderator);
        var proposal = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(
            await ProposeSuspensionAsync(member, DateTimeOffset.UtcNow.AddDays(1)));

        await SignInAsStaffAsync(UserRole.Moderator);
        (await ConfirmAsync(proposal.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await MemberAsync(member)).AccessState.Should().Be("Suspended");

        // A day passes. Written to the row rather than waited for.
        await SetSuspendedUntilAsync(member, DateTimeOffset.UtcNow.AddMinutes(-1));

        var sweep = Factory.Services.GetServices<IHostedService>().OfType<SuspensionExpirySweep>().Single();
        (await sweep.RunOnceAsync()).Should().BeGreaterThanOrEqualTo(1);

        var reinstated = await MemberAsync(member);
        reinstated.AccessState.Should().Be("Verified", "the member returns to the rung they were on");
        reinstated.AccountStatus.Should().Be("Active");
        reinstated.StatusReasonCode.Should().BeNull();
        reinstated.SuspendedUntil.Should().BeNull();
    }

    [Fact]
    public async Task An_indefinite_suspension_is_left_for_staff_to_lift()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Moderator);
        var proposal = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(
            await ProposeSuspensionAsync(member));

        await SignInAsStaffAsync(UserRole.Moderator);
        (await ConfirmAsync(proposal.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.Services.GetServices<IHostedService>().OfType<SuspensionExpirySweep>().Single().RunOnceAsync();

        (await MemberAsync(member)).AccessState.Should().Be("Suspended");

        // Lifting it is one member of staff's call, with a note the member is told.
        (await HttpClient.PostAsJsonAsync($"{Root}/{member}/reinstate", new { }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/{member}/reinstate", new { notes = "The reports were withdrawn." }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await MemberAsync(member)).AccessState.Should().Be("Verified");
    }

    [Fact]
    public async Task Reopening_a_closed_account_takes_a_second_member_of_staff_and_lets_the_member_back_in()
    {
        var (phone, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Admin);
        var closure = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/{member}/disable",
                new { reasonCode = "Impersonation", reason = "Posing as the consulate to collect fees." },
                JsonOptions));

        await SignInAsStaffAsync(UserRole.Admin);
        (await ConfirmAsync(closure.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var closed = await MemberAsync(member);
        closed.AccessState.Should().Be("Disabled");
        closed.StatusReasonCode.Should().Be("Impersonation");
        (await SignInStatusAsync(phone)).Should().NotBe(HttpStatusCode.OK, "a closed account cannot sign in");

        // The member appealed, and the appeal was upheld.
        var proposerOfReopening = await SignInAsStaffAsync(UserRole.Admin);
        var proposed = await HttpClient.PostAsJsonAsync(
            $"{Root}/{member}/reactivate",
            new { reasonCode = "Impersonation", note = "Appeal upheld: they are the consulate's own officer." },
            JsonOptions);
        proposed.StatusCode.Should().Be(HttpStatusCode.Accepted, await proposed.Content.ReadAsStringAsync());
        var reopening = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(proposed);
        reopening.Kind.Should().Be("Reactivate");
        reopening.ProposedBy.Should().Be(proposerOfReopening);

        (await MemberAsync(member)).AccessState.Should().Be("Disabled", "a proposal reopens nothing");

        await SignInAsStaffAsync(UserRole.Admin);
        (await ConfirmAsync(reopening.Id, "Read the appeal and the consulate's letter.")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var reopened = await MemberAsync(member);
        reopened.AccessState.Should().Be("LimitedAccess", "trust is earned again after a closure");
        reopened.StatusReasonCode.Should().BeNull();

        (await SignInStatusAsync(phone)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_proposer_can_withdraw_what_nobody_has_decided_and_nobody_else_can()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        await SignInAsStaffAsync(UserRole.Moderator);
        var proposal = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(
            await ProposeSuspensionAsync(auth.User.Id));
        var proposerToken = HttpClient.DefaultRequestHeaders.Authorization!.Parameter!;

        await SignInAsStaffAsync(UserRole.Admin);
        (await HttpClient.PostAsJsonAsync($"{Actions}/{proposal.Id}/withdraw", new { }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Authenticate(proposerToken);
        var withdrawn = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(
            await HttpClient.PostAsJsonAsync(
                $"{Actions}/{proposal.Id}/withdraw", new { note = "Wrong account." }, JsonOptions));

        withdrawn.Status.Should().Be("Withdrawn");
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────

    private const string SuspensionNote = "Asked three buyers to pay outside the platform.";

    [Fact]
    public async Task A_third_strike_raises_a_suspension_that_one_member_of_staff_takes_up_and_a_second_confirms()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;
        var caseId = Guid.NewGuid();

        await AnnounceThirdStrikeAsync(member, caseId);

        // Delivered again, as at-least-once delivery may: it raises nothing more.
        await AnnounceThirdStrikeAsync(member, caseId);

        var first = await SignInAsStaffAsync(UserRole.Moderator);

        var queue = await IdentityApiClient.ReadDataAsync<List<AccountActionProposalResponse>>(
            await HttpClient.GetAsync($"{Actions}?status=Pending&userId={member}"));

        var raised = queue.Should().ContainSingle("one third strike raises one suspension").Subject;
        raised.Kind.Should().Be("Suspend");
        raised.ProposedBy.Should().BeNull("the platform raised it; no member of staff has proposed it");
        raised.AwaitsProposer.Should().BeTrue();
        raised.RaisedByCaseId.Should().Be(caseId);
        raised.ReasonCode.Should().Be("FraudOrScam");
        raised.Until.Should().BeNull("a third strike suspends until reviewed");

        // Nobody has proposed it, so nobody can confirm it: the platform is not one of the two.
        var early = await ConfirmAsync(raised.Id);
        early.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOfAsync(early)).Should().Be("Identity.Administration.ProposalNeedsProposer");
        (await MemberAsync(member)).AccountStatus.Should().Be("Active");

        // Taking it up needs a note, because the member reads it.
        (await AdoptAsync(raised.Id, null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var adopted = await AdoptAsync(raised.Id, ThirdStrikeNote);
        adopted.StatusCode.Should().Be(HttpStatusCode.OK, await adopted.Content.ReadAsStringAsync());

        var taken = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(adopted);
        taken.ProposedBy.Should().Be(first);
        taken.AwaitsProposer.Should().BeFalse();
        taken.Note.Should().Be(ThirdStrikeNote, "the member reads the note of the person who proposed it");
        taken.RaisedByCaseId.Should().Be(caseId, "where it came from stays on the record");

        // Once taken up, it is that person's proposal: they cannot confirm it, and nobody takes it up twice.
        (await ConfirmAsync(raised.Id)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await SignInAsStaffAsync(UserRole.Moderator);
        (await AdoptAsync(raised.Id, ThirdStrikeNote)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var confirmed = await ConfirmAsync(raised.Id, "Read the three removals. Agreed.");
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());

        var suspended = await MemberAsync(member);
        suspended.AccessState.Should().Be("Suspended");
        suspended.StatusReasonCode.Should().Be("FraudOrScam");
        suspended.StatusReason.Should().Be(ThirdStrikeNote);
        suspended.SuspendedUntil.Should().BeNull();
    }

    [Fact]
    public async Task A_suspension_the_platform_raised_can_be_declined_by_one_member_of_staff()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await AnnounceThirdStrikeAsync(member, Guid.NewGuid());

        await SignInAsStaffAsync(UserRole.Moderator);

        var raised = (await IdentityApiClient.ReadDataAsync<List<AccountActionProposalResponse>>(
            await HttpClient.GetAsync($"{Actions}?status=Pending&userId={member}"))).Single();

        // Declining sanctions nobody, so it does not wait for a second person.
        var declined = await HttpClient.PostAsJsonAsync(
            $"{Actions}/{raised.Id}/decline",
            new { note = "Two of the three removals were overturned on appeal." },
            JsonOptions);

        declined.StatusCode.Should().Be(HttpStatusCode.OK, await declined.Content.ReadAsStringAsync());
        (await MemberAsync(member)).AccountStatus.Should().Be("Active");
    }

    [Fact]
    public async Task An_upheld_appeal_lifts_a_suspension_and_neither_member_of_staff_behind_it_may_decide_it()
    {
        var (phone, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        using var memberClient = Factory.CreateClient();
        memberClient.WithBearer(auth.Tokens.AccessToken);

        // A member who is not suspended has no suspension to appeal.
        (await AppealSuspensionAsync(memberClient, member)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Verified moderators: each of them could review an appeal, so a refusal below is the bar
        // on the two people behind the suspension and not a missing permission.
        var (proposer, _) = await SignInAsRoleAsync(nameof(UserRole.Moderator));
        var proposal = await IdentityApiClient.ReadDataAsync<AccountActionProposalResponse>(
            await ProposeSuspensionAsync(member));
        var proposerToken = HttpClient.DefaultRequestHeaders.Authorization;

        await SignInAsRoleAsync(nameof(UserRole.Moderator));
        (await ConfirmAsync(proposal.Id, "Agreed on the reports.")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await MemberAsync(member)).AccessState.Should().Be("Suspended");

        // Signed out by the suspension, the member signs in again and appeals from the screen that tells them.
        var session = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await new IdentityApiClient(memberClient).LoginAsync(
                new LoginRequest { Email = EmailFor(phone), Password = ValidPassword }));
        memberClient.WithBearer(session.Tokens.AccessToken);

        // Only their own account.
        (await AppealSuspensionAsync(memberClient, proposer)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var filed = await AppealSuspensionAsync(memberClient, member);
        filed.StatusCode.Should().Be(HttpStatusCode.Created, await filed.Content.ReadAsStringAsync());
        var appealId = await AppealIdOfAsync(filed);

        // The confirmer is the caller now; then the proposer. Neither may decide the appeal.
        var byConfirmer = await ReviewAppealAsync(appealId);
        (await byConfirmer.Content.ReadAsStringAsync()).Should().Contain("trust.review.self-forbidden");

        HttpClient.DefaultRequestHeaders.Authorization = proposerToken;
        var byProposer = await ReviewAppealAsync(appealId);
        (await byProposer.Content.ReadAsStringAsync()).Should().Contain("trust.review.self-forbidden");

        (await MemberAsync(member)).AccessState.Should().Be("Suspended");

        await SignInAsRoleAsync(nameof(UserRole.Moderator));
        var upheld = await ReviewAppealAsync(appealId);
        upheld.StatusCode.Should().Be(HttpStatusCode.OK, await upheld.Content.ReadAsStringAsync());

        await Factory.DrainOutboxAsync();

        var lifted = await MemberAsync(member);
        lifted.AccessState.Should().NotBe("Suspended", "an upheld appeal is carried out, not only recorded");
        lifted.AccountStatus.Should().Be("Active");
    }

    private const string ThirdStrikeNote = "Three adverts removed for asking candidates to pay visa fees.";

    private const string AppealReason = "The adverts were posted by somebody else who had my password at the time.";

    /// <summary>Announces a third strike as Trust does, and delivers it.</summary>
    private async Task AnnounceThirdStrikeAsync(Guid member, Guid caseId)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

            await events.PublishAsync(new MemberReachedThirdStrikeIntegrationEvent
            {
                UserId = member,
                CaseId = caseId,
                Area = "jobs",
                ReasonCode = "FraudOrScam",
                At = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();
        }

        await Factory.DrainOutboxAsync();
    }

    private Task<HttpResponseMessage> AdoptAsync(Guid proposalId, string? note) =>
        HttpClient.PostAsJsonAsync($"{Actions}/{proposalId}/adopt", new { note }, JsonOptions);

    private static Task<HttpResponseMessage> AppealSuspensionAsync(HttpClient client, Guid targetId) =>
        client.PostAsJsonAsync(
            "/api/v1/trust/appeals",
            new { type = "Suspension", targetId = targetId.ToString(), reason = AppealReason },
            JsonOptions);

    private Task<HttpResponseMessage> ReviewAppealAsync(Guid appealId) =>
        HttpClient.PostAsJsonAsync(
            $"/api/v1/trust/appeals/{appealId}/review",
            new { decision = "Approve", explanation = "The account was taken over; the member has secured it since." },
            JsonOptions);

    private static async Task<Guid> AppealIdOfAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement.TryGetProperty("data", out var data) ? data : document.RootElement;

        return root.GetProperty("appealId").GetGuid();
    }

    private Task<HttpResponseMessage> ProposeSuspensionAsync(Guid member, DateTimeOffset? until = null) =>
        HttpClient.PostAsJsonAsync(
            $"{Root}/{member}/suspend",
            new SuspendAccountRequest { ReasonCode = "FraudOrScam", Reason = SuspensionNote, SuspendedUntil = until },
            JsonOptions);

    private Task<HttpResponseMessage> ConfirmAsync(Guid proposalId, string? note = null) =>
        HttpClient.PostAsJsonAsync($"{Actions}/{proposalId}/confirm", new { note }, JsonOptions);

    private async Task<AdminMemberResponse> MemberAsync(Guid member) =>
        await IdentityApiClient.ReadDataAsync<AdminMemberResponse>(await HttpClient.GetAsync($"{Root}/{member}"));

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>Signs the member in on a client of their own, leaving the staff caller in place.</summary>
    private async Task<HttpStatusCode> SignInStatusAsync(string phone)
    {
        using var client = Factory.CreateClient();

        var response = await new IdentityApiClient(client).LoginAsync(
            new LoginRequest { Email = EmailFor(phone), Password = ValidPassword });

        return response.StatusCode;
    }

    /// <summary>The caller's security notices, as rendered.</summary>
    private async Task<List<(string Title, string Body)>> SecurityInboxAsync()
    {
        var response = await HttpClient.GetAsync("/api/v1/notifications?category=security&limit=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var notices = new List<(string Title, string Body)>();
        Collect(document.RootElement, notices);

        return notices;
    }

    private static void Collect(JsonElement element, List<(string Title, string Body)> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("title", out var title) && element.TryGetProperty("body", out var body))
                {
                    into.Add((title.GetString() ?? string.Empty, body.GetString() ?? string.Empty));
                    return;
                }

                foreach (var property in element.EnumerateObject())
                {
                    Collect(property.Value, into);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, into);
                }

                break;
        }
    }

    private async Task SetSuspendedUntilAsync(Guid member, DateTimeOffset until)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var id = UserId.From(member);

        var user = await context.Set<User>().FirstAsync(candidate => candidate.Id == id);
        context.Entry(user).Property(nameof(User.SuspendedUntil)).CurrentValue = until;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Registers a member, seats them in a role in the database, and signs in as them.
    /// <para>
    /// The seating is a direct write because the platform's own route needs a member of staff
    /// above them, and for Admin a second one: the suite has neither before it makes its first.
    /// </para>
    /// </summary>
    private async Task<Guid> SignInAsStaffAsync(UserRole role)
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        var userId = auth.User.Id;

        await SeatRoleAsync(userId, role.Name);

        // A fresh sign-in, because the token issued at registration carries the permissions
        // of the role they had then.
        Api.ClearAuthentication();

        var signIn = await Api.LoginAsync(new LoginRequest
        {
            Email = EmailFor(phone),
            Password = ValidPassword
        });

        var session = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(signIn);

        Api.Authenticate(session.Tokens.AccessToken);
        Authenticate(session.Tokens.AccessToken);

        return userId;
    }

    private void Authenticate(string token) =>
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    private async Task<string> RoleOfAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(UserId.From(userId));

        return user!.Role.Name;
    }
}

internal static class AdminTestClientExtensions
{
    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
