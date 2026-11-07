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
/// A housing listing on Trust's one moderation case loop, end to end (7 Nov 2026).
/// <para>
/// A member's report reaches a Trust case (before this date nothing raised the housing report
/// event, so no housing report reached anything); staff decide only on the case; the decision is
/// carried out on the property by the housing module, in its own moderation state; a removal needs
/// a second member of staff; and the module's report rows close when the case is decided.
/// </para>
/// </summary>
[Collection(HousingCollection.Name)]
public sealed class HousingModerationCaseTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/housing";
    private const string Trust = "/api/v1/trust";
    private const string ContentType = "housing-listing";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HousingModerationCaseTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_report_opens_a_case_a_hide_takes_the_property_down_and_a_restore_brings_it_back()
    {
        var home = await PublishedHomeAsync();
        (await AnonymousStatusAsync(home.ListingId)).Should().Be(HttpStatusCode.OK);

        await ReportAsync(home.ListingId, "Scam", "Wants a deposit by wire before any viewing.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");

        var item = await CaseForAsync(home.ListingId);
        item.ReasonCode.Should().Be("FraudOrScam", "a housing scam report is a fraud or scam");
        item.ReportCount.Should().Be(1);

        await AssignAsync(item.ItemId);

        var hidden = await DecideAsync(item.ItemId, "HidePendingReview", "FraudOrScam",
            "Asks for a wired deposit before anyone has seen the flat.");
        hidden.StatusCode.Should().Be(HttpStatusCode.OK, await hidden.Content.ReadAsStringAsync());

        (await AnonymousStatusAsync(home.ListingId)).Should().Be(
            HttpStatusCode.NotFound, "the housing module carried the hide out");
        (await StaffViewAsync(home.ListingId)).ModerationState.Should().Be("Hidden");

        await Factory.DrainOutboxAsync();
        (await ReportStatusesAsync(home.ListingId)).Should().Equal(
            new[] { "Upheld" }, "the decision closed the module's report, upheld");

        // The landlord asks for a review, and a moderator restores it.
        Api.Authenticate(home.OwnerToken);
        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await SignInAsRoleAsync("Moderator");
        await AssignAsync(item.ItemId);

        var restored = await DecideAsync(item.ItemId, "Restore", "Other",
            "The landlord now asks for the deposit only at signing, in person.");
        restored.StatusCode.Should().Be(HttpStatusCode.OK, await restored.Content.ReadAsStringAsync());

        (await AnonymousStatusAsync(home.ListingId)).Should().Be(HttpStatusCode.OK);
        (await StaffViewAsync(home.ListingId)).ModerationState.Should().Be("Clear");
    }

    [Fact]
    public async Task A_removal_takes_effect_only_when_a_second_member_of_staff_confirms_it()
    {
        var home = await PublishedHomeAsync();

        await ReportAsync(home.ListingId, "Scam", "The photographs are from a hotel website.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync(home.ListingId);
        await AssignAsync(item.ItemId);

        var proposed = await DecideAsync(item.ItemId, "Remove", "FraudOrScam",
            "Photographs copied from a hotel; the address does not exist.");
        proposed.StatusCode.Should().Be(HttpStatusCode.OK, await proposed.Content.ReadAsStringAsync());

        var pending = await ReadDataAsync<CaseShape>(proposed);
        pending.CurrentOutcome.Should().Be("HidePendingReview", "until it is confirmed, it is only hidden");
        pending.SecondReview.Should().Be("RemovalProposal");
        (await StaffViewAsync(home.ListingId)).ModerationState.Should().Be("Hidden");

        await SignInAsRoleAsync("Moderator");

        var confirmed = await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{item.ItemId}/second-review",
            new { confirm = true, note = "Searched the photographs myself; they are the hotel's. Agreed." });
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());

        var removed = await ReadDataAsync<CaseShape>(confirmed);
        removed.CurrentOutcome.Should().Be("Remove");
        removed.Decisions.Select(decision => decision.Kind).Should().Equal("Proposal", "Confirmation");

        (await StaffViewAsync(home.ListingId)).ModerationState.Should().Be(
            "Suspended", "a removal is the state only staff lift");
        (await AnonymousStatusAsync(home.ListingId)).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task No_action_takes_the_reports_flag_off_and_dismisses_the_report()
    {
        var home = await PublishedHomeAsync();

        await ReportAsync(home.ListingId, "Scam", "The rent seems far too low for the area.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        (await StaffViewAsync(home.ListingId)).ModerationState.Should().Be(
            "Flagged", "a scam report flags the property on the first report");

        var item = await CaseForAsync(home.ListingId);
        await AssignAsync(item.ItemId);

        var cleared = await DecideAsync(item.ItemId, "NoAction", "Other",
            "The rent matches the landlord's other homes on the same street.");
        cleared.StatusCode.Should().Be(HttpStatusCode.OK, await cleared.Content.ReadAsStringAsync());

        (await StaffViewAsync(home.ListingId)).ModerationState.Should().Be("Clear");
        (await AnonymousStatusAsync(home.ListingId)).Should().Be(HttpStatusCode.OK);

        await Factory.DrainOutboxAsync();
        (await ReportStatusesAsync(home.ListingId)).Should().Equal("Dismissed");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private sealed record PublishedHome(Guid ListingId, string OwnerToken);

    /// <summary>
    /// A verified member with a landlord profile, registered over HTTP, and one published property
    /// of theirs written directly: the create route's body is not what this suite is about.
    /// </summary>
    private async Task<PublishedHome> PublishedHomeAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var registered = await HttpClient.PostAsJsonAsync(
            $"{Root}/landlords",
            new { displayName = "Case Loop Lettings", kind = "Individual", countryCode = "MU" },
            JsonOptions);
        registered.StatusCode.Should().Be(
            HttpStatusCode.Created, await registered.Content.ReadAsStringAsync());

        var landlord = await ReadDataAsync<LandlordShape>(registered);

        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;
        var title = $"Garden flat {Guid.NewGuid():N}";

        var listing = HousingListing.Draft(
            HousingListingId.New(),
            LandlordProfileId.From(landlord.Id),
            UserId.From(auth.User.Id),
            HousingTitle.Create(title).Value,
            HousingSlug.Create(title),
            HousingDescription.Create("A home seeded for the moderation case tests.").Value,
            "apartment",
            HousingPrice.Create(15_000m, "MUR", RentPeriod.PerMonth, null).Value,
            HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
            HousingAvailability.Create(totalUnits: 1).Value,
            HousingLocationContext.Create("MU", cityName: "Quatre Bornes").Value,
            PropertyAddress.Undisclosed,
            StayPolicy.Create(
                acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
            now).Value;

        listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now).IsSuccess.Should().BeTrue();
        listing.Publish(now).IsSuccess.Should().BeTrue();

        dbContext.Set<HousingListing>().Add(listing);
        await dbContext.SaveChangesAsync();

        return new PublishedHome(listing.Id.Value, auth.Tokens.AccessToken);
    }

    private async Task ReportAsync(Guid listingId, string reason, string detail)
    {
        var (_, reporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporter.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/listings/{listingId}/reports", new { reason, detail }, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpStatusCode> AnonymousStatusAsync(Guid listingId)
    {
        using var anonymous = Factory.CreateClient();

        return (await anonymous.GetAsync($"{Root}/listings/{listingId}")).StatusCode;
    }

    private async Task<ListingShape> StaffViewAsync(Guid listingId) =>
        await ReadDataAsync<ListingShape>(await HttpClient.GetAsync($"{Root}/listings/{listingId}"));

    private async Task<List<string>> ReportStatusesAsync(Guid listingId)
    {
        var reports = await ReadDataAsync<List<ReportShape>>(
            await HttpClient.GetAsync($"{Root}/moderation/reports?listingId={listingId}&openOnly=false"));

        return [.. reports.Select(report => report.Status)];
    }

    private async Task<CaseShape> CaseForAsync(Guid listingId) =>
        await ReadDataAsync<CaseShape>(
            await HttpClient.GetAsync($"{Trust}/moderation/cases?contentType={ContentType}&contentId={listingId}"));

    private async Task AssignAsync(Guid itemId) =>
        (await HttpClient.PostAsync($"{Trust}/moderation/queue/{itemId}/assign", content: null))
            .EnsureSuccessStatusCode();

    private Task<HttpResponseMessage> DecideAsync(Guid itemId, string outcome, string reasonCode, string note) =>
        HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{itemId}/decision", new { outcome, reasonCode, note });

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record LandlordShape(Guid Id);

    private sealed record ListingShape(Guid Id, string? ModerationState);

    private sealed record ReportShape(Guid Id, string Status);

    private sealed record DecisionShape(string? Kind, string? Outcome);

    private sealed record CaseShape(
        Guid ItemId,
        string? ReasonCode,
        int ReportCount,
        string? CurrentOutcome,
        string SecondReview,
        List<DecisionShape> Decisions);
}
