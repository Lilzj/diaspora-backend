using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Email verification end to end — 5 Oct 2026.
/// <para>
/// The test host has no email transport, so sending answers 503 and a link is minted directly
/// with the deployment's own signer. Everything after the email is the real path: the public page,
/// its button, the command, the database, and the member's view of their address.
/// </para>
/// </summary>
public sealed class EmailVerificationTests : IdentityTestBase
{
    private const string Email = "/api/v1/auth/email";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EmailVerificationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_new_member_sees_their_address_as_not_yet_verified()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var email = await DataAsync(await HttpClient.GetAsync(Email));

        email.GetProperty("address").GetString().Should().Be(auth.User.Email);
        email.GetProperty("isVerified").GetBoolean().Should().BeFalse();
        email.GetProperty("deliveryState").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task Without_an_email_transport_sending_says_so_rather_than_pretending()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsync($"{Email}/verification", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Opening_the_link_does_not_verify_and_pressing_the_button_does()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var token = Mint(auth.User.Id, auth.User.Email!);

        // What a mail scanner does: fetch the link.
        var page = await HttpClient.GetAsync($"/email/confirm?token={Uri.EscapeDataString(token)}");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync()).Should().Contain("Confirm my email");
        (await DataAsync(await HttpClient.GetAsync(Email))).GetProperty("isVerified").GetBoolean().Should().BeFalse();

        // What the member does: press the button.
        var confirmed = await Press(token);
        confirmed.Should().Contain("Email confirmed");
        (await DataAsync(await HttpClient.GetAsync(Email))).GetProperty("isVerified").GetBoolean().Should().BeTrue();

        // Pressing it again is harmless.
        (await Press(token)).Should().Contain("Already confirmed");
    }

    [Fact]
    public async Task A_forged_or_damaged_link_verifies_nothing_and_says_it_has_expired()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var token = Mint(auth.User.Id, auth.User.Email!);

        var damaged = token[..^4] + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        (await Press(damaged)).Should().Contain("expired or is not valid");
        (await DataAsync(await HttpClient.GetAsync(Email))).GetProperty("isVerified").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_link_for_another_address_does_not_verify_the_one_on_the_account()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var token = Mint(auth.User.Id, "someone.else@example.com");

        (await Press(token)).Should().Contain("old address");
        (await DataAsync(await HttpClient.GetAsync(Email))).GetProperty("isVerified").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task The_page_escapes_whatever_it_is_given()
    {
        var page = await HttpClient.GetAsync("/email/confirm?token=%22%3E%3Cscript%3Ealert(1)%3C%2Fscript%3E");

        var html = await page.Content.ReadAsStringAsync();
        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public async Task The_app_can_confirm_a_link_it_intercepted()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        var token = Mint(auth.User.Id, auth.User.Email!);

        // Anonymous on purpose: the app may hold the link on a device where nobody is signed in.
        Api.ClearAuthentication();
        var first = await DataAsync(await HttpClient.PostAsJsonAsync("/api/v1/auth/email/confirm", new { token }));
        var second = await DataAsync(await HttpClient.PostAsJsonAsync("/api/v1/auth/email/confirm", new { token }));
        var forged = await DataAsync(await HttpClient.PostAsJsonAsync("/api/v1/auth/email/confirm", new { token = "abc" }));

        first.GetProperty("outcome").GetString().Should().Be("Verified");
        second.GetProperty("outcome").GetString().Should().Be("AlreadyVerified");
        forged.GetProperty("outcome").GetString().Should().Be("Invalid");
    }

    [Fact]
    public async Task An_unconfigured_deployment_vouches_for_no_app()
    {
        var response = await HttpClient.GetAsync("/.well-known/assetlinks.json");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private string Mint(Guid userId, string address)
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IEmailVerificationLinks>()
            .Create(UserId.From(userId), address).Token;
    }

    private async Task<string> Press(string token)
    {
        using var form = new FormUrlEncodedContent([new KeyValuePair<string, string>("token", token)]);
        var response = await HttpClient.PostAsync("/email/confirm", form);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"the request should succeed, got {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }
}
