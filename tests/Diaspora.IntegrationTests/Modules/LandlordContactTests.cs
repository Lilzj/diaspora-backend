using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A landlord's phone number and email, and whether the platform says them out loud.
/// <para>
/// <b>The setting could be written and never read.</b> <c>ShowContactPublicly</c> reached the
/// domain from <c>POST /housing/landlords</c> and <c>PATCH /housing/landlords/me</c>, was stored
/// on <see cref="Diaspora.Modules.Housing.Domain.ValueObjects.LandlordContact"/>, and appeared in
/// no response at all — so a landlord could not see what they had chosen, and a client that
/// offered them the choice had nothing to show it back with. Setting it a second time was the
/// only way to find out what it was.
/// </para>
/// <para>
/// It is the landlord's setting rather than a fact about them, which is why a stranger gets
/// <c>null</c> and not <c>false</c>: "they have not published a number" and "I am not allowed to
/// know whether they have" are different answers, and only the first is a stranger's business.
/// </para>
/// </summary>
public sealed class LandlordContactTests : IdentityTestBase
{
    private const string Root = "/api/v1/housing";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public LandlordContactTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_landlord_who_published_their_details_is_told_so_and_a_stranger_sees_them()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var landlordId = await RegisterLandlordAsync(
            run, phone: "+230 5791 2345", email: $"open-{run}@example.com", showPublicly: true);

        var own = await ReadProfileAsync(landlordId);

        own.IsOwner.Should().BeTrue();
        own.ShowContactPublicly.Should().BeTrue("the landlord asked for the details to be shown");
        own.PhoneNumber.Should().Be("+230 5791 2345");
        own.Email.Should().Be($"open-{run}@example.com");

        Api.ClearAuthentication();
        var theirs = await ReadProfileAsync(landlordId);

        theirs.PhoneNumber.Should().Be("+230 5791 2345", "the landlord published them");
        theirs.ShowContactPublicly.Should().BeNull("the setting belongs to the landlord");
    }

    /// <summary>
    /// The default, and the one that matters: nothing published. A stranger gets no number, and
    /// the landlord still gets to see that this is what they chose.
    /// </summary>
    [Fact]
    public async Task A_landlord_who_kept_their_details_private_still_sees_them_and_nobody_else_does()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var landlordId = await RegisterLandlordAsync(
            run, phone: "+230 5791 2345", email: $"quiet-{run}@example.com", showPublicly: false);

        var own = await ReadProfileAsync(landlordId);

        own.ShowContactPublicly.Should().BeFalse();
        own.PhoneNumber.Should().Be("+230 5791 2345", "it is their own number");

        Api.ClearAuthentication();
        var theirs = await ReadProfileAsync(landlordId);

        theirs.PhoneNumber.Should().BeNull("the landlord did not publish it");
        theirs.Email.Should().BeNull();
        theirs.ShowContactPublicly.Should().BeNull();
    }

    /// <summary>
    /// The round trip a create screen needs: set it once, read it back, change it, read it back.
    /// Without the read this is unfalsifiable from the client's side.
    /// </summary>
    [Fact]
    public async Task Changing_the_setting_changes_what_comes_back()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var landlordId = await RegisterLandlordAsync(
            run, phone: "+230 5791 2345", email: $"turn-{run}@example.com", showPublicly: false);

        var changed = await HttpClient.PatchAsync(
            $"{Root}/landlords/me",
            JsonContent.Create(
                new { phoneNumber = "+230 5700 0001", showContactPublicly = true }, options: JsonOptions));

        changed.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await ReadProfileAsync(landlordId);

        after.ShowContactPublicly.Should().BeTrue();
        after.PhoneNumber.Should().Be("+230 5700 0001");
        after.Email.Should().Be($"turn-{run}@example.com", "a patch that named one field changed one");
    }

    private async Task<Guid> RegisterLandlordAsync(
        string run, string phone, string email, bool showPublicly)
    {
        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/landlords",
            new
            {
                displayName = $"Contact Lettings {run}",
                kind = "Individual",
                countryCode = "MU",
                phoneNumber = phone,
                email,
                showContactPublicly = showPublicly
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await IdentityApiClient.ReadDataAsync<ProfileShape>(response)).Id;
    }

    private async Task<ProfileShape> ReadProfileAsync(Guid landlordId) =>
        await IdentityApiClient.ReadDataAsync<ProfileShape>(
            await HttpClient.GetAsync($"{Root}/landlords/{landlordId}"));

    private sealed record ProfileShape
    {
        public Guid Id { get; init; }

        public bool IsOwner { get; init; }

        public string? PhoneNumber { get; init; }

        public string? Email { get; init; }

        public bool? ShowContactPublicly { get; init; }

        public int Revision { get; init; }
    }
}
