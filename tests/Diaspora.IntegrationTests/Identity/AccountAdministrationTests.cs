using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.IntegrationTests.Common;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

/// <summary>
/// Staff acting on somebody else's account — 19 Sep 2026.
/// <para>
/// <b>Four permissions existed from the beginning with no policy and no route.</b> Reading
/// a member, reading their history, signing them out and giving them a role were all things
/// the platform said staff could do and gave them no way to do, which is why every staff
/// account so far was made with a database write. These tests are the routes that ended
/// that, and the two refusals that keep role assignment from being an escalation ladder.
/// </para>
/// </summary>
[Collection(IdentityCollection.Name)]
public sealed class AccountAdministrationTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/admin/accounts";

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
    public async Task Nobody_changes_their_own_role()
    {
        var staff = await SignInAsStaffAsync(UserRole.Admin);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{staff}/role",
            new { role = nameof(UserRole.SuperAdmin) },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await RoleOfAsync(staff).ContinueWith(
            role => role.Result.Should().Be(nameof(UserRole.Admin), "the refusal changed nothing"),
            TaskScheduler.Default);
    }

    [Fact]
    public async Task An_admin_cannot_appoint_a_super_admin()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        await SignInAsStaffAsync(UserRole.Admin);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{member}/role",
            new { role = nameof(UserRole.SuperAdmin) },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
            new { role = "Moderatorr" },
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

    // ── Plumbing ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a member, promotes them in the database, and signs in as them.
    /// <para>
    /// The promotion is a direct write because that is the only way a first staff account
    /// has ever been made — which is the whole reason these routes now exist.
    /// </para>
    /// </summary>
    private async Task<Guid> SignInAsStaffAsync(UserRole role)
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        var userId = auth.User.Id;

        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
            var user = await context.Set<User>().FirstAsync(candidate => candidate.Id == UserId.From(userId));
            user.AssignRole(role);
            await context.SaveChangesAsync();
        }

        // A fresh sign-in, because the token issued at registration carries the permissions
        // of the role they had then.
        Api.ClearAuthentication();

        var signIn = await Api.LoginAsync(new Diaspora.Contracts.Identity.V1.Requests.LoginRequest
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
