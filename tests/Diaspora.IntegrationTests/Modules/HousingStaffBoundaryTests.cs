using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Staff and a landlord's property — 7 Nov 2026.
/// <para>
/// <b>Until this date the owner check began "if the caller is staff, allow".</b> A moderator or
/// administrator could revise any member's property, its amenities, rules, availability and
/// photographs, publish it, archive it, mark it let, delete and restore it, all through the
/// landlord's own routes and with no report, reason or notice. The owner's rule is that staff never
/// act on a member's content through the member's routes; they act through moderation, which is
/// recorded on the moderation trail.
/// </para>
/// <para>
/// Each refusal asserts the module's not-owned code as well as the status. A staff member is a
/// verified member, so the route's permission lets them in; a bare 403 could be the permission
/// layer and would prove nothing about the owner check.
/// </para>
/// </summary>
[Collection(HousingCollection.Name)]
public sealed class HousingStaffBoundaryTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/housing";

    private const string NotOwned = "housing.listing.not-owned";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HousingStaffBoundaryTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The rule: an edit and a state change, both refused to staff on a property they do not let —
    /// and the same two routes, on the same property, still working for its landlord afterwards, so
    /// the refusal is about who is asking rather than a route that no longer works.
    /// </summary>
    [Theory]
    [InlineData("Moderator")]
    [InlineData("Admin")]
    public async Task Staff_cannot_edit_or_archive_a_landlords_property_and_the_landlord_still_can(
        string role)
    {
        var (landlordToken, listingId) = await LandlordWithDraftAsync();

        await SignInAsRoleAsync(role);

        var edit = await HttpClient.PatchAsJsonAsync(
            $"{Root}/listings/{listingId}",
            new { title = "Retitled by a member of staff" },
            JsonOptions);

        edit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await edit.Content.ReadAsStringAsync()).Should().Contain(
            NotOwned, "the owner check refused it, not the route's permission");

        var archive = await HttpClient.PostAsJsonAsync(
            $"{Root}/listings/{listingId}/archive",
            new { temporarily = false, reason = "Archived by staff." },
            JsonOptions);

        archive.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await archive.Content.ReadAsStringAsync()).Should().Contain(NotOwned);

        var delete = await HttpClient.DeleteAsync($"{Root}/listings/{listingId}");

        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await delete.Content.ReadAsStringAsync()).Should().Contain(NotOwned);

        // Staff still see it — reading is how a moderator reviews — and nothing they tried landed.
        var seen = await ReadListingAsync(listingId);

        seen.Status.Should().Be("Draft", "the archive was refused");
        seen.Title.Should().NotBe("Retitled by a member of staff", "the edit was refused");

        // The landlord, on the same property and the same routes.
        Api.Authenticate(landlordToken);

        var ownEdit = await HttpClient.PatchAsJsonAsync(
            $"{Root}/listings/{listingId}",
            new { title = "Bright two-bedroom flat near the market" },
            JsonOptions);

        ownEdit.StatusCode.Should().Be(HttpStatusCode.OK, await ownEdit.Content.ReadAsStringAsync());

        var ownArchive = await HttpClient.PostAsJsonAsync(
            $"{Root}/listings/{listingId}/archive",
            new { temporarily = false, reason = "Let privately." },
            JsonOptions);

        ownArchive.StatusCode.Should().Be(
            HttpStatusCode.OK, await ownArchive.Content.ReadAsStringAsync());

        var mine = await ReadListingAsync(listingId);

        mine.Status.Should().Be("Archived");
        mine.Title.Should().Be("Bright two-bedroom flat near the market");
    }

    /// <summary>
    /// What replaces the back door: the moderation route, which a moderator still holds.
    /// </summary>
    [Fact]
    public async Task A_moderator_still_hides_a_property_through_the_moderation_route()
    {
        var (_, listingId) = await LandlordWithDraftAsync();

        await SignInAsRoleAsync("Moderator");

        var hidden = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/listings/{listingId}",
            new { action = "hide", reason = "Reported as a duplicate advert; under review." },
            JsonOptions);

        hidden.StatusCode.Should().Be(HttpStatusCode.OK, await hidden.Content.ReadAsStringAsync());

        (await ReadListingAsync(listingId)).ModerationState.Should().Be("Hidden");
    }

    /// <summary>
    /// Dismissing a report takes off the report's own flag and nothing else (7 Nov 2026). It used to
    /// clear the listing outright, so dismissing a weak report on a suspended scam put the scam back
    /// in search.
    /// </summary>
    [Fact]
    public async Task Dismissing_a_report_does_not_lift_an_unrelated_suspension()
    {
        var (_, listingId) = await LandlordWithDraftAsync();
        var (_, reporter) = await RegisterVerifiedMemberAsync();

        await SignInAsRoleAsync("Moderator");

        var suspended = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/listings/{listingId}",
            new { action = "suspend", reason = "Deposit demanded before any viewing; confirmed scam." },
            JsonOptions);
        suspended.StatusCode.Should().Be(HttpStatusCode.OK, await suspended.Content.ReadAsStringAsync());

        var reportId = await FileReportDirectlyAsync(listingId, reporter.User.Id);

        var dismissed = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/reports/{reportId}/resolve",
            new { upheld = false, resolution = "This report was about the photos, which are fine." },
            JsonOptions);
        dismissed.StatusCode.Should().Be(HttpStatusCode.OK, await dismissed.Content.ReadAsStringAsync());

        (await ReadListingAsync(listingId)).ModerationState.Should().Be(
            "Suspended", "a dismissed report is no decision about the suspension");
    }

    /// <summary>Housing moderation never asked for a reason; every action needs one now (7 Nov 2026).</summary>
    [Fact]
    public async Task A_moderation_action_without_a_reason_is_refused()
    {
        var (_, listingId) = await LandlordWithDraftAsync();

        await SignInAsRoleAsync("Moderator");

        var refused = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/listings/{listingId}", new { action = "hide" }, JsonOptions);

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadListingAsync(listingId)).ModerationState.Should().NotBe("Hidden");
    }

    private async Task<Guid> FileReportDirectlyAsync(Guid listingId, Guid reporterId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var listing = await dbContext.Set<HousingListing>().FindAsync(HousingListingId.From(listingId));

        var report = Diaspora.Modules.Housing.Domain.Reports.HousingReport.File(
            HousingReportId.From(Guid.NewGuid()),
            listing!.Id,
            listing.LandlordId,
            UserId.From(reporterId),
            HousingReportReason.Misleading,
            "The photos look like a different flat.",
            DateTimeOffset.UtcNow).Value;

        dbContext.Add(report);
        await dbContext.SaveChangesAsync();

        return report.Id.Value;
    }

    // ── Seeding ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A verified member with a landlord profile, registered over HTTP, and one draft property of
    /// theirs written directly: the create route's body is not what this suite is about.
    /// </summary>
    private async Task<(string LandlordToken, Guid ListingId)> LandlordWithDraftAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var registered = await HttpClient.PostAsJsonAsync(
            $"{Root}/landlords",
            new { displayName = "Boundary Lettings", kind = "Individual", countryCode = "MU" },
            JsonOptions);

        registered.StatusCode.Should().Be(
            HttpStatusCode.Created, await registered.Content.ReadAsStringAsync());

        var landlord = await IdentityApiClient.ReadDataAsync<LandlordShape>(registered);

        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;
        var title = $"Two-bedroom flat {Guid.NewGuid():N}";

        var listing = HousingListing.Draft(
            HousingListingId.New(),
            LandlordProfileId.From(landlord.Id),
            UserId.From(auth.User.Id),
            HousingTitle.Create(title).Value,
            HousingSlug.Create(title),
            HousingDescription.Create("A home seeded for the staff boundary tests.").Value,
            "apartment",
            HousingPrice.Create(15_000m, "MUR", RentPeriod.PerMonth, null).Value,
            HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
            HousingAvailability.Create(totalUnits: 1).Value,
            HousingLocationContext.Create("MU", cityName: "Rose Hill").Value,
            PropertyAddress.Undisclosed,
            StayPolicy.Create(
                acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
            now).Value;

        dbContext.Set<HousingListing>().Add(listing);

        await dbContext.SaveChangesAsync();

        return (auth.Tokens.AccessToken, listing.Id.Value);
    }

    private async Task<ListingShape> ReadListingAsync(Guid listingId)
    {
        var response = await HttpClient.GetAsync($"{Root}/listings/{listingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await IdentityApiClient.ReadDataAsync<ListingShape>(response);
    }

    // ── Wire shapes, re-declared by hand ────────────────────────────────────────────────────

    private sealed record LandlordShape
    {
        public Guid Id { get; init; }
    }

    private sealed record ListingShape
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string? ModerationState { get; init; }
    }
}
