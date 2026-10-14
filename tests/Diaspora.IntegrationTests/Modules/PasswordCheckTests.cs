using System.Net;
using System.Net.Http.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The password step asks the server before moving on (14 Oct 2026). Registration used to
/// report a refused password three screens after it was typed, at the location screen.
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class PasswordCheckTests : PooledIdentityTestBase
{
    public PasswordCheckTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_password_registration_would_accept_is_accepted()
    {
        var response = await HttpClient.PostAsJsonAsync("/api/v1/auth/password-check",
            new { password = "Harbour-Lantern-47", email = "someone@example.com", fullName = "Ada Mensah" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Diaspora2026!x", "too common")]
    [InlineData("short", "at least")]
    public async Task A_password_registration_would_refuse_is_refused_with_the_same_message(
        string password, string expected)
    {
        var response = await HttpClient.PostAsJsonAsync("/api/v1/auth/password-check",
            new { password, email = "someone@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(expected);
    }
}
