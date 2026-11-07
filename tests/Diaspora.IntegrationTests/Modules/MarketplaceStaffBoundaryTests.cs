using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Staff and a seller's listing — 7 Nov 2026.
/// <para>
/// <b>Until this date the owner check began "if the caller is staff, allow".</b> A moderator or
/// administrator could edit, retag, restock, publish, archive, mark sold, delete and restore any
/// member's listing through the seller's own routes, with no report, reason or notice — and the
/// change looked exactly like the seller's own. The owner's rule is that staff never act on a
/// member's content through the member's routes; they act through moderation, which is recorded as
/// moderation. Since 7 Nov 2026 that moderation is a Trust case: the marketplace moderation routes no
/// longer act on a listing.
/// </para>
/// <para>
/// Each refusal asserts the module's not-owned code as well as the status. A staff member is a
/// verified member, so the route's permission lets them in; a bare 403 could be the permission
/// layer and would prove nothing about the owner check.
/// </para>
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class MarketplaceStaffBoundaryTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/marketplace";

    private const string Trust = "/api/v1/trust";

    private const string NotOwned = "marketplace.listing.not-owned";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MarketplaceStaffBoundaryTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The rule: an edit and a state change, both refused to staff on a listing they do not own —
    /// and the same two routes, on the same listing, still working for its seller afterwards, so
    /// the refusal is about who is asking rather than a route that no longer works.
    /// </summary>
    [Theory]
    [InlineData("Moderator")]
    [InlineData("Admin")]
    public async Task Staff_cannot_edit_or_archive_a_sellers_listing_and_the_seller_still_can(
        string role)
    {
        var (sellerToken, listingId) = await SellerWithDraftAsync();

        await SignInAsRoleAsync(role);

        var edit = await HttpClient.PutAsJsonAsync(
            $"{Root}/listings/{listingId}/tags",
            new { tags = new[] { "edited-by-staff" } },
            JsonOptions);

        edit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await edit.Content.ReadAsStringAsync()).Should().Contain(
            NotOwned, "the owner check refused it, not the route's permission");

        var archive = await HttpClient.PostAsJsonAsync(
            $"{Root}/listings/{listingId}/archive",
            new { reason = "Archived by staff." },
            JsonOptions);

        archive.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await archive.Content.ReadAsStringAsync()).Should().Contain(NotOwned);

        var delete = await HttpClient.DeleteAsync($"{Root}/listings/{listingId}");

        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await delete.Content.ReadAsStringAsync()).Should().Contain(NotOwned);

        // Staff still see it — reading is how a moderator reviews — and nothing they tried landed.
        var seen = await ReadListingAsync(listingId);

        seen.Status.Should().Be("Draft", "the archive was refused");
        seen.Tags.Should().NotContain("edited-by-staff", "the edit was refused");

        // The seller, on the same listing and the same routes.
        Api.Authenticate(sellerToken);

        var ownEdit = await HttpClient.PutAsJsonAsync(
            $"{Root}/listings/{listingId}/tags",
            new { tags = new[] { "vintage" } },
            JsonOptions);

        ownEdit.StatusCode.Should().Be(HttpStatusCode.OK, await ownEdit.Content.ReadAsStringAsync());

        var ownArchive = await HttpClient.PostAsJsonAsync(
            $"{Root}/listings/{listingId}/archive",
            new { reason = "Sold elsewhere." },
            JsonOptions);

        ownArchive.StatusCode.Should().Be(
            HttpStatusCode.OK, await ownArchive.Content.ReadAsStringAsync());

        var mine = await ReadListingAsync(listingId);

        mine.Status.Should().Be("Archived");
        mine.Tags.Should().Contain("vintage");
    }

    /// <summary>
    /// What replaces the back door: a Trust moderation case, here one staff open with no report.
    /// Until 7 Nov 2026 the marketplace moderation route hid a listing on one moderator's word; it is
    /// gone, and the decision on the case is carried out on the listing by the marketplace module.
    /// </summary>
    [Fact]
    public async Task A_moderator_hides_a_listing_only_through_a_trust_case()
    {
        var (_, listingId) = await SellerWithDraftAsync();

        await SignInAsRoleAsync("Moderator");

        var oldRoute = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/listings/{listingId}",
            new { action = "hide", reason = "Reported as counterfeit; under review." },
            JsonOptions);

        oldRoute.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed], "the module route no longer acts");
        (await ReadListingAsync(listingId)).ModerationState.Should().NotBe("Hidden");

        var opened = await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/cases",
            new
            {
                contentType = "marketplace-listing",
                contentId = listingId,
                reasonCode = "FraudOrScam",
                note = "The photographs show a counterfeit of a branded frame."
            });
        opened.StatusCode.Should().Be(HttpStatusCode.Created, await opened.Content.ReadAsStringAsync());

        var item = await IdentityApiClient.ReadDataAsync<CaseShape>(opened);

        (await HttpClient.PostAsync($"{Trust}/moderation/queue/{item.ItemId}/assign", content: null))
            .EnsureSuccessStatusCode();

        var hidden = await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{item.ItemId}/decision",
            new
            {
                outcome = "HidePendingReview",
                reasonCode = "FraudOrScam",
                note = "Reported as counterfeit; under review."
            });

        hidden.StatusCode.Should().Be(HttpStatusCode.OK, await hidden.Content.ReadAsStringAsync());

        (await ReadListingAsync(listingId)).ModerationState.Should().Be("Hidden");
    }

    // ── Seeding ──────────────────────────────────────────────────────────────────────────────

    /// <summary>A verified member with a seller profile and one draft listing.</summary>
    private async Task<(string SellerToken, Guid ListingId)> SellerWithDraftAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var seller = await HttpClient.PostAsJsonAsync(
            $"{Root}/sellers",
            new { displayName = "Boundary Seller", countryCode = "MU", kind = "Individual" },
            JsonOptions);

        seller.StatusCode.Should().Be(HttpStatusCode.Created, await seller.Content.ReadAsStringAsync());

        var created = await HttpClient.PostAsJsonAsync(
            $"{Root}/listings",
            new
            {
                title = $"Road bicycle {Guid.NewGuid():N}",
                description = "Steel frame, new tyres, rides well.",
                categoryKey = "bicycles",
                countryCode = "MU",
                listingType = "FixedPrice",
                condition = "Good",
                price = 4500,
                currency = "MUR",
                offersPickup = true,
                mediaIds = new[] { Guid.NewGuid() }
            },
            JsonOptions);

        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var listing = await IdentityApiClient.ReadDataAsync<ListingShape>(created);

        return (auth.Tokens.AccessToken, listing.ListingId);
    }

    private async Task<ListingShape> ReadListingAsync(Guid listingId)
    {
        var response = await HttpClient.GetAsync($"{Root}/listings/{listingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await IdentityApiClient.ReadDataAsync<ListingShape>(response);
    }

    // ── Wire shapes, re-declared by hand ────────────────────────────────────────────────────

    private sealed record ListingShape
    {
        public Guid ListingId { get; init; }

        public string Status { get; init; } = string.Empty;

        public List<string> Tags { get; init; } = [];

        public string? ModerationState { get; init; }
    }

    private sealed record CaseShape
    {
        public Guid ItemId { get; init; }
    }
}
