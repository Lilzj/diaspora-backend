using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Infrastructure.Identity.EmailVerification;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.IntegrationTests.Common;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

/// <summary>
/// Changing the sign-in address end to end — 20 Oct 2026. An account-takeover surface, so every
/// rule is asserted over HTTP against the real handlers, page and database: the password, the link
/// going only to the new address, the old address staying the login until it is pressed, the
/// notices to the old address, the taken address that must not be an oracle, the dead links, and
/// the sessions cut when it completes.
/// </summary>
[Collection(IdentityCollection.Name)]
public sealed class EmailChangeTests : PooledIdentityTestBase
{
    private const string Email = "/api/v1/auth/email";

    public EmailChangeTests(CustomWebApplicationFactory factory) : base(factory) { }

    private CapturedEmailChangeMail Mail => Factory.EmailChangeMail;

    // ── Asking ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_wrong_password_changes_nothing_and_sends_nothing()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var target = NewAddress();

        var response = await RequestChange(target, "Not-The-Password-1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("IncorrectCurrentPassword");
        Mail.To(target).Should().BeEmpty();
        Mail.To(EmailFor(phone)).Should().BeEmpty();
        (await AddressOnAccount()).Should().Be(EmailFor(phone));
    }

    [Fact]
    public async Task Without_a_session_nothing_can_be_asked()
    {
        Api.ClearAuthentication();

        var response = await RequestChange(NewAddress(), ValidPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_link_goes_to_the_new_address_only_and_the_old_one_is_told_without_seeing_it()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var oldAddress = EmailFor(phone);
        var target = NewAddress();

        var state = await DataAsync(await RequestChange(target, ValidPassword));

        state.GetProperty("address").GetString().Should().Be(oldAddress);
        state.GetProperty("pendingAddress").GetString().Should().Be(target);

        Mail.To(target).Should().ContainSingle().Which.Kind.Should().Be(CapturedEmailChangeMail.Link);

        var notice = Mail.To(oldAddress).Should().ContainSingle().Subject;
        notice.Kind.Should().Be(CapturedEmailChangeMail.Requested);
        notice.Token.Should().BeNull("the old address must never receive the link");
        notice.MaskedNewAddress.Should().NotBe(target).And.NotContain(target.Split('@')[0]);
    }

    [Fact]
    public async Task Until_the_link_is_pressed_the_old_address_is_still_the_login()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var target = NewAddress();

        await DataAsync(await RequestChange(target, ValidPassword));

        // Opening the link is what a mail scanner does; it must change nothing.
        var page = await HttpClient.GetAsync($"/email/change?token={Uri.EscapeDataString(Mail.LinkFor(target))}");
        (await page.Content.ReadAsStringAsync()).Should().Contain("Confirm my new email");

        (await AddressOnAccount()).Should().Be(EmailFor(phone));
        (await SignIn(EmailFor(phone))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SignIn(target)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Asking_for_the_address_already_on_the_account_is_refused()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await RequestChange(EmailFor(phone).ToUpperInvariant(), ValidPassword);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Mail.To(EmailFor(phone)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_second_request_inside_the_minute_is_refused_with_a_wait()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await DataAsync(await RequestChange(NewAddress(), ValidPassword));
        var second = await RequestChange(NewAddress(), ValidPassword);

        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter.Should().NotBeNull();
    }

    // ── A taken address ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_address_another_account_uses_gets_the_same_answer_and_its_owner_gets_a_warning_not_a_link()
    {
        var (otherPhone, _) = await RegisterAndVerifyAsync();
        var taken = EmailFor(otherPhone);

        var (phoneA, authA) = await RegisterAndVerifyAsync();
        var (_, authB) = await RegisterAndVerifyAsync();
        var free = NewAddress();

        Api.Authenticate(authA.Tokens.AccessToken);
        var takenResponse = await RequestChange(taken, ValidPassword);
        var takenBody = await DataAsync(takenResponse);

        Api.Authenticate(authB.Tokens.AccessToken);
        var freeResponse = await RequestChange(free, ValidPassword);
        var freeBody = await DataAsync(freeResponse);

        // The asker cannot tell the two apart: same status, same fields, the address echoed back.
        takenResponse.StatusCode.Should().Be(freeResponse.StatusCode);
        PropertyNames(takenBody).Should().BeEquivalentTo(PropertyNames(freeBody));
        takenBody.GetProperty("pendingAddress").GetString().Should().Be(taken);
        takenBody.GetProperty("cooldownSeconds").GetInt32().Should().Be(freeBody.GetProperty("cooldownSeconds").GetInt32());

        // The owner of the taken address is told; nobody gets a link to it.
        Mail.To(taken).Should().ContainSingle().Which.Kind.Should().Be(CapturedEmailChangeMail.Unavailable);
        Mail.To(EmailFor(phoneA)).Should().ContainSingle().Which.Kind.Should().Be(CapturedEmailChangeMail.Requested);
    }

    [Fact]
    public async Task A_link_whose_address_was_taken_in_the_meantime_changes_nothing()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var target = NewAddress();
        await DataAsync(await RequestChange(target, ValidPassword));
        var token = Mail.LinkFor(target);

        // Another account takes the address before the link is pressed.
        var (_, rival) = await RegisterAndVerifyAsync();
        await TakeAddress(rival.User.Id, target);

        (await Press(token)).Should().Contain("already in use");
        Api.Authenticate(auth.Tokens.AccessToken);
        (await AddressOnAccount()).Should().Be(EmailFor(phone));
    }

    // ── Pressing the link ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pressing_the_link_moves_the_login_verifies_it_and_tells_the_old_address()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var oldAddress = EmailFor(phone);
        var target = NewAddress();
        await DataAsync(await RequestChange(target, ValidPassword));
        var token = Mail.LinkFor(target);

        (await Press(token)).Should().Contain("Email address changed");

        var email = await DataAsync(await HttpClient.GetAsync(Email));
        email.GetProperty("address").GetString().Should().Be(target);
        email.GetProperty("isVerified").GetBoolean().Should().BeTrue();

        (await SignIn(target)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SignIn(oldAddress)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var sentToOld = Mail.To(oldAddress);
        var completed = sentToOld[^1];
        completed.Kind.Should().Be(CapturedEmailChangeMail.Completed);
        completed.MaskedNewAddress.Should().NotBe(target);

        // The same link again is harmless.
        (await Press(token)).Should().Contain("Already changed");
    }

    [Fact]
    public async Task Every_other_session_is_signed_out_and_the_one_that_asked_is_not()
    {
        var (phone, asking) = await RegisterAndVerifyAsync();
        var other = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(await SignIn(EmailFor(phone)));

        Api.Authenticate(asking.Tokens.AccessToken);
        var target = NewAddress();
        await DataAsync(await RequestChange(target, ValidPassword));

        // Pressed from a browser with no session, as from a mail client.
        Api.ClearAuthentication();
        (await Press(Mail.LinkFor(target))).Should().Contain("Email address changed");

        Api.Authenticate(asking.Tokens.AccessToken);
        (await HttpClient.GetAsync(Email)).StatusCode.Should().Be(HttpStatusCode.OK);

        Api.Authenticate(other.Tokens.AccessToken);
        (await HttpClient.GetAsync(Email)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Api.ClearAuthentication();
        (await Api.RefreshAsync(other.Tokens.RefreshToken)).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task The_app_can_confirm_a_link_it_intercepted()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var target = NewAddress();
        await DataAsync(await RequestChange(target, ValidPassword));
        var token = Mail.LinkFor(target);

        Api.ClearAuthentication();
        var first = await DataAsync(await HttpClient.PostAsJsonAsync($"{Email}/change/confirm", new { token }));
        var second = await DataAsync(await HttpClient.PostAsJsonAsync($"{Email}/change/confirm", new { token }));
        var forged = await DataAsync(await HttpClient.PostAsJsonAsync($"{Email}/change/confirm", new { token = "abc" }));

        first.GetProperty("outcome").GetString().Should().Be("Changed");
        second.GetProperty("outcome").GetString().Should().Be("AlreadyChanged");
        forged.GetProperty("outcome").GetString().Should().Be("Invalid");
    }

    // ── Dead links ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_tampered_link_changes_nothing()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var target = NewAddress();
        await DataAsync(await RequestChange(target, ValidPassword));
        var token = Mail.LinkFor(target);
        var damaged = token[..^4] + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        (await Press(damaged)).Should().Contain("expired or is not valid");
        (await AddressOnAccount()).Should().Be(EmailFor(phone));
    }

    [Fact]
    public async Task An_expired_link_changes_nothing()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // Minted three hours ago with the deployment's own key: well past its two-hour life.
        var jwt = Factory.Services.GetRequiredService<IOptions<JwtOptions>>();
        var past = new EmailChangeLinks(jwt, new FakeClock(DateTimeOffset.UtcNow.AddHours(-3)));
        var (token, _) = past.Create(new EmailChangeClaim(
            UserId.From(auth.User.Id), EmailFor(phone), NewAddress(), await StampOf(auth.User.Id), null));

        (await Press(token)).Should().Contain("expired or is not valid");
        (await AddressOnAccount()).Should().Be(EmailFor(phone));
    }

    [Fact]
    public async Task Changing_the_password_cancels_an_outstanding_link()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var target = NewAddress();
        await DataAsync(await RequestChange(target, ValidPassword));

        // What the "a change was requested" notice tells the old address's owner to do.
        var changed = await Api.ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = ValidPassword,
            NewPassword = "Another-Horse-Battery-8"
        });
        changed.EnsureSuccessStatusCode();

        (await Press(Mail.LinkFor(target))).Should().Contain("expired or is not valid");

        // Read from the store: the password change rotated the stamp, which retired this test's token.
        (await StoredAddress(auth.User.Id)).Should().Be(EmailFor(phone));
    }

    [Fact]
    public async Task Once_one_change_completes_every_other_outstanding_link_is_dead()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        var first = NewAddress();
        var second = NewAddress();

        // Two links minted for the same starting address, as two requests a minute apart would be.
        string firstToken, secondToken;
        using (var scope = Factory.Services.CreateScope())
        {
            var links = scope.ServiceProvider.GetRequiredService<IEmailChangeLinks>();
            var stamp = await StampOf(auth.User.Id);
            var from = auth.User.Email!;
            firstToken = links.Create(new EmailChangeClaim(UserId.From(auth.User.Id), from, first, stamp, null)).Token;
            secondToken = links.Create(new EmailChangeClaim(UserId.From(auth.User.Id), from, second, stamp, null)).Token;
        }

        (await Press(firstToken)).Should().Contain("Email address changed");
        (await Press(secondToken)).Should().Contain("expired or is not valid");
    }

    // ── The old door ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_profile_route_no_longer_moves_the_sign_in_address()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await Api.UpdateProfileAsync(new UpdateProfileRequest
        {
            FullName = "Amara Okafor",
            Nationality = "NG",
            CountryOfResidence = "CA",
            PreferredLanguage = "en",
            PreferredTimeZone = "UTC",
            Email = NewAddress()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EmailChangeNeedsConfirmation");
        (await AddressOnAccount()).Should().Be(EmailFor(phone));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private static string NewAddress() => $"moved-{Guid.NewGuid():N}@diaspora.test";

    private Task<HttpResponseMessage> RequestChange(string address, string password) =>
        HttpClient.PutAsJsonAsync(Email, new ChangeEmailRequest { Address = address, CurrentPassword = password });

    private Task<HttpResponseMessage> SignIn(string address) =>
        Api.LoginAsync(new LoginRequest { Email = address, Password = ValidPassword });

    private async Task<string?> AddressOnAccount() =>
        (await DataAsync(await HttpClient.GetAsync(Email))).GetProperty("address").GetString();

    private async Task<string?> StoredAddress(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Diaspora.Domain.Identity.Repositories.IUserRepository>();
        return (await users.GetByIdAsync(UserId.From(userId)))!.Email?.Value;
    }

    private async Task<int> StampOf(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Diaspora.Domain.Identity.Repositories.IUserRepository>();
        return (await users.GetByIdAsync(UserId.From(userId)))!.Security.SecurityStamp;
    }

    private async Task TakeAddress(Guid userId, string address)
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Diaspora.Domain.Identity.Repositories.IUserRepository>();
        var user = await users.GetByIdAsync(UserId.From(userId));
        user!.ChangeEmail(Diaspora.SharedKernel.ValueObjects.EmailAddress.Create(address), DateTimeOffset.UtcNow);
        users.Update(user);
        await scope.ServiceProvider.GetRequiredService<Diaspora.Domain.Abstractions.IUnitOfWork>().SaveChangesAsync();
    }

    private async Task<string> Press(string token)
    {
        using var form = new FormUrlEncodedContent([new KeyValuePair<string, string>("token", token)]);
        var response = await HttpClient.PostAsync("/email/change", form);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name);

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"the request should succeed, got {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }
}
