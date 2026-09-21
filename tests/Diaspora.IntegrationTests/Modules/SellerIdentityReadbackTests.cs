using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A seller's legal name and company number come back to the seller.
/// <para>
/// <b>They were write-only, and that made the platform's own update contract unusable.</b> Both
/// reached the domain from <c>POST /marketplace/sellers</c> and <c>PUT /marketplace/sellers/me</c>
/// and appeared in no response. <c>PUT</c> is a replace, not a merge —
/// <c>UpdateSellerCommandHandler</c> passes <c>request.LegalName</c>, <c>request.Bio</c> and the
/// rest straight into the aggregate — so a client that read a profile, changed a telephone number
/// and sent it back had no way to carry the legal identity through, and would have blanked the
/// very fields Trust verified the business by.
/// </para>
/// <para>
/// They belong to the seller, not to a buyer browsing storefronts: a trading name is what a
/// storefront is called, and a company registration number is not part of that.
/// </para>
/// </summary>
public sealed class SellerIdentityReadbackTests : IdentityTestBase
{
    private const string Root = "/api/v1/marketplace";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SellerIdentityReadbackTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_business_seller_can_read_back_the_identity_it_registered_with()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var sellerId = await RegisterSellerAsync(run);

        var own = await ReadAsync(sellerId);

        own.CanManage.Should().BeTrue();
        own.LegalName.Should().Be($"Nwosu Electronics Limited {run}");
        own.RegistrationNumber.Should().Be($"RC-{run}");

        // Everything else a replace has to send back, in the same answer.
        own.Bio.Should().Be("Refurbished laptops, three-month warranty.");
        own.Website.Should().Be("https://nwosu.example.com");
        own.CountryCode.Should().Be("NG");
    }

    /// <summary>
    /// A buyer sees the storefront's name. The company number behind it is the seller's business
    /// and Trust's, and a listing page has never had a reason to carry it.
    /// </summary>
    [Fact]
    public async Task A_buyer_browsing_the_storefront_sees_the_trading_name_and_nothing_behind_it()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var sellerId = await RegisterSellerAsync(run);

        Api.ClearAuthentication();
        var theirs = await ReadAsync(sellerId);

        theirs.DisplayName.Should().Be($"Nwosu Electronics {run}", "the storefront has a name");
        theirs.LegalName.Should().BeNull();
        theirs.RegistrationNumber.Should().BeNull();
    }

    private async Task<Guid> RegisterSellerAsync(string run)
    {
        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/sellers",
            new
            {
                displayName = $"Nwosu Electronics {run}",
                kind = "Business",
                countryCode = "NG",
                legalName = $"Nwosu Electronics Limited {run}",
                registrationNumber = $"RC-{run}",
                bio = "Refurbished laptops, three-month warranty.",
                website = "https://nwosu.example.com",
                phone = "+234 800 000 0000",
                email = $"sales-{run}@example.com",
                contactPreference = "Phone"
            },
            JsonOptions);

        response.StatusCode.Should().Be(
            HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return (await IdentityApiClient.ReadDataAsync<SellerShape>(response)).SellerId;
    }

    private async Task<SellerShape> ReadAsync(Guid sellerId) =>
        await IdentityApiClient.ReadDataAsync<SellerShape>(
            await HttpClient.GetAsync($"{Root}/sellers/{sellerId}"));

    private sealed record SellerShape
    {
        public Guid SellerId { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string? LegalName { get; init; }

        public string? RegistrationNumber { get; init; }

        public string? Bio { get; init; }

        public string? Website { get; init; }

        public string CountryCode { get; init; } = string.Empty;

        public bool? CanManage { get; init; }
    }
}
