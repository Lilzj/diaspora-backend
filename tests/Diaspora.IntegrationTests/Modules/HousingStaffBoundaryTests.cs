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
/// recorded on the moderation trail. Since 7 Nov 2026 that moderation is a Trust case: the housing
/// moderation routes no longer act on a property.
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

    private const string Trust = "/api/v1/trust";

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
    /// What replaces the back door: a Trust moderation case, here one staff open with no report.
    /// Until 7 Nov 2026 the housing moderation route hid a property on one moderator's word; it is
    /// gone, and the decision on the case is carried out on the property by the housing module.
    /// </summary>
    [Fact]
    public async Task A_moderator_hides_a_property_only_through_a_trust_case()
    {
        var (_, listingId) = await LandlordWithDraftAsync();

        await SignInAsRoleAsync("Moderator");

        var oldRoute = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/listings/{listingId}",
            new { action = "hide", reason = "Reported as a duplicate advert; under review." },
            JsonOptions);

        oldRoute.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed], "the module route no longer acts");
        (await ReadListingAsync(listingId)).ModerationState.Should().NotBe("Hidden");

        var item = await OpenCaseAsync(listingId, "Duplicate", "The same flat is advertised twice by this landlord.");
        await AssignAsync(item.ItemId);

        var hidden = await DecideAsync(item.ItemId, "HidePendingReview", "Duplicate",
            "Reported as a duplicate advert; under review.");
        hidden.StatusCode.Should().Be(HttpStatusCode.OK, await hidden.Content.ReadAsStringAsync());

        (await ReadListingAsync(listingId)).ModerationState.Should().Be("Hidden");
    }

    /// <summary>
    /// A weak report does not lift a standing removal (7 Nov 2026).
    /// <para>
    /// This was "dismissing a report does not lift an unrelated suspension": dismissing a report on
    /// the housing route cleared the property outright, so a moderator dismissing a weak report on a
    /// suspended scam put the scam back in search. Dismissal is now a Trust case decision, and the
    /// rule is the case's: a removal confirmed by a second member of staff stands; a later report
    /// joins the decided case, a no-action decision on it is refused, and the property stays
    /// suspended. (The housing module's own half of the rule, that no action clears only an
    /// automatic flag, is covered by the case loop tests.)
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_later_report_cannot_be_dismissed_off_a_standing_removal()
    {
        var (_, listingId) = await LandlordWithDraftAsync(publish: true);

        var (_, firstReporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(firstReporter.Tokens.AccessToken);

        var reported = await HttpClient.PostAsJsonAsync(
            $"{Root}/listings/{listingId}/reports",
            new { reason = "Scam", detail = "Deposit demanded before any viewing." },
            JsonOptions);
        reported.StatusCode.Should().Be(HttpStatusCode.Created, await reported.Content.ReadAsStringAsync());

        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync(listingId);
        await AssignAsync(item.ItemId);

        (await DecideAsync(item.ItemId, "Remove", "FraudOrScam",
            "Deposit demanded before any viewing; confirmed scam.")).EnsureSuccessStatusCode();

        await SignInAsRoleAsync("Moderator");
        (await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{item.ItemId}/second-review",
            new { confirm = true, note = "Spoke to two seekers who were asked for the same deposit." }))
            .EnsureSuccessStatusCode();

        (await ReadListingAsync(listingId)).ModerationState.Should().Be("Suspended");

        // A weak report about the photographs, filed directly: a suspended property is not
        // reportable through the member's route, because a member can no longer see it.
        var (_, laterReporter) = await RegisterVerifiedMemberAsync();
        await FileReportDirectlyAsync(listingId, laterReporter.User.Id);
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var joined = await CaseForAsync(listingId);
        joined.ItemId.Should().Be(item.ItemId, "a later report joins the case that decided the removal");
        joined.ReportCount.Should().Be(2);

        await AssignAsync(item.ItemId);

        var dismissed = await DecideAsync(item.ItemId, "NoAction", "Other",
            "This report was about the photos, which are fine.");
        dismissed.StatusCode.Should().Be(HttpStatusCode.Conflict, "a case is decided once");

        (await ReadListingAsync(listingId)).ModerationState.Should().Be(
            "Suspended", "a later report is no decision about the removal");
    }

    /// <summary>
    /// Housing moderation never asked for a reason (7 Nov 2026). A decision is now a Trust case
    /// decision, and the case refuses one without a note the owner can read.
    /// </summary>
    [Fact]
    public async Task A_case_decision_without_a_note_is_refused()
    {
        var (_, listingId) = await LandlordWithDraftAsync();

        await SignInAsRoleAsync("Moderator");

        var item = await OpenCaseAsync(listingId, "Spam", "Advert repeats a phone number in every line.");
        await AssignAsync(item.ItemId);

        (await DecideAsync(item.ItemId, "HidePendingReview", "Spam", "")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await DecideAsync(item.ItemId, "HidePendingReview", "Spam", "Spam.")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "a note is at least ten characters");

        (await ReadListingAsync(listingId)).ModerationState.Should().NotBe("Hidden");
    }

    private async Task FileReportDirectlyAsync(Guid listingId, Guid reporterId)
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
            2,
            DateTimeOffset.UtcNow).Value;

        dbContext.Add(report);
        await dbContext.SaveChangesAsync();
    }

    private async Task<CaseShape> OpenCaseAsync(Guid listingId, string reasonCode, string note)
    {
        var opened = await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/cases",
            new { contentType = "housing-listing", contentId = listingId, reasonCode, note });

        opened.StatusCode.Should().Be(HttpStatusCode.Created, await opened.Content.ReadAsStringAsync());

        return await IdentityApiClient.ReadDataAsync<CaseShape>(opened);
    }

    private async Task<CaseShape> CaseForAsync(Guid listingId) =>
        await IdentityApiClient.ReadDataAsync<CaseShape>(
            await HttpClient.GetAsync($"{Trust}/moderation/cases?contentType=housing-listing&contentId={listingId}"));

    private async Task AssignAsync(Guid itemId) =>
        (await HttpClient.PostAsync($"{Trust}/moderation/queue/{itemId}/assign", content: null))
            .EnsureSuccessStatusCode();

    private Task<HttpResponseMessage> DecideAsync(Guid itemId, string outcome, string reasonCode, string note) =>
        HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{itemId}/decision", new { outcome, reasonCode, note });

    // ── Seeding ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A verified member with a landlord profile, registered over HTTP, and one draft property of
    /// theirs written directly: the create route's body is not what this suite is about. Published
    /// when <paramref name="publish"/> is set, so that a member can see and report it.
    /// </summary>
    private async Task<(string LandlordToken, Guid ListingId)> LandlordWithDraftAsync(bool publish = false)
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

        if (publish)
        {
            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now).IsSuccess.Should().BeTrue();
            listing.Publish(now).IsSuccess.Should().BeTrue();
        }

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

    private sealed record CaseShape
    {
        public Guid ItemId { get; init; }

        public int ReportCount { get; init; }
    }
}
