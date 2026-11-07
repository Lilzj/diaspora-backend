using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Marketplace.Domain.Reports;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A marketplace listing on Trust's one moderation case loop, end to end (7 Nov 2026).
/// <para>
/// A member's report reaches a Trust case; staff decide only on the case; the decision is carried
/// out on the listing by the marketplace module, in its own moderation state; a removal needs a
/// second member of staff; and the module's report rows close when the case is decided.
/// </para>
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class MarketplaceModerationCaseTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/marketplace";
    private const string Trust = "/api/v1/trust";
    private const string ContentType = "marketplace-listing";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MarketplaceModerationCaseTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_report_opens_a_case_a_hide_takes_the_listing_down_and_a_restore_brings_it_back()
    {
        var listing = await PublishedListingAsync();
        (await AnonymousStatusAsync(listing.ListingId)).Should().Be(HttpStatusCode.OK);

        await ReportAsync(listing.ListingId, "Fraud", "Asked me to pay by gift card before viewing.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");

        var item = await CaseForAsync(listing.ListingId);
        item.ReasonCode.Should().Be("FraudOrScam", "a marketplace fraud report is a fraud or scam");
        item.ReportCount.Should().Be(1);

        await AssignAsync(item.ItemId);

        var hidden = await DecideAsync(item.ItemId, "HidePendingReview", "FraudOrScam",
            "Asks buyers to pay with gift cards before any viewing.");
        hidden.StatusCode.Should().Be(HttpStatusCode.OK, await hidden.Content.ReadAsStringAsync());

        (await AnonymousStatusAsync(listing.ListingId)).Should().Be(
            HttpStatusCode.NotFound, "the marketplace module carried the hide out");
        (await StaffViewAsync(listing.ListingId)).ModerationState.Should().Be("Hidden");

        await Factory.DrainOutboxAsync();
        (await ReportStatusesAsync(listing.ListingId)).Should().Equal(
            new[] { "Actioned" }, "the decision closed the module's report, upheld");

        // The seller asks for a review, and a moderator restores it.
        Api.Authenticate(listing.OwnerToken);
        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await SignInAsRoleAsync("Moderator");
        await AssignAsync(item.ItemId);

        var restored = await DecideAsync(item.ItemId, "Restore", "Other",
            "The seller removed the gift card request and takes cash on collection.");
        restored.StatusCode.Should().Be(HttpStatusCode.OK, await restored.Content.ReadAsStringAsync());

        (await AnonymousStatusAsync(listing.ListingId)).Should().Be(HttpStatusCode.OK);
        (await StaffViewAsync(listing.ListingId)).ModerationState.Should().Be("Clear");
    }

    [Fact]
    public async Task A_removal_takes_effect_only_when_a_second_member_of_staff_confirms_it()
    {
        var listing = await PublishedListingAsync();

        await ReportAsync(listing.ListingId, "Counterfeit", "Sold as a branded frame; the welds say otherwise.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        var item = await CaseForAsync(listing.ListingId);
        item.ReasonCode.Should().Be("FraudOrScam", "a counterfeit lists the case on the first report");

        await AssignAsync(item.ItemId);

        var proposed = await DecideAsync(item.ItemId, "Remove", "FraudOrScam",
            "A counterfeit frame passed off as a branded one.");
        proposed.StatusCode.Should().Be(HttpStatusCode.OK, await proposed.Content.ReadAsStringAsync());

        var pending = await ReadDataAsync<CaseShape>(proposed);
        pending.CurrentOutcome.Should().Be("HidePendingReview", "until it is confirmed, it is only hidden");
        pending.SecondReview.Should().Be("RemovalProposal");
        (await StaffViewAsync(listing.ListingId)).ModerationState.Should().Be("Hidden");

        await SignInAsRoleAsync("Moderator");

        var confirmed = await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{item.ItemId}/second-review",
            new { confirm = true, note = "Checked the photographs against the maker's own. Agreed." });
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());

        var removed = await ReadDataAsync<CaseShape>(confirmed);
        removed.CurrentOutcome.Should().Be("Remove");
        removed.Decisions.Select(decision => decision.Kind).Should().Equal("Proposal", "Confirmation");

        (await StaffViewAsync(listing.ListingId)).ModerationState.Should().Be(
            "Suspended", "a removal is the state only staff lift");
        (await AnonymousStatusAsync(listing.ListingId)).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task No_action_takes_the_reports_flag_off_and_dismisses_the_report()
    {
        var listing = await PublishedListingAsync();

        await ReportAsync(listing.ListingId, "Fraud", "I think the price is too low to be real.");
        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Moderator");
        (await StaffViewAsync(listing.ListingId)).ModerationState.Should().Be(
            "Flagged", "a fraud report flags the listing on the first report");

        var item = await CaseForAsync(listing.ListingId);
        await AssignAsync(item.ItemId);

        var cleared = await DecideAsync(item.ItemId, "NoAction", "Other",
            "Price is in line with others in the category.");
        cleared.StatusCode.Should().Be(HttpStatusCode.OK, await cleared.Content.ReadAsStringAsync());

        (await StaffViewAsync(listing.ListingId)).ModerationState.Should().Be("Clear");
        (await AnonymousStatusAsync(listing.ListingId)).Should().Be(HttpStatusCode.OK);

        await Factory.DrainOutboxAsync();
        (await ReportStatusesAsync(listing.ListingId)).Should().Equal("Dismissed");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private sealed record PublishedListing(Guid ListingId, string OwnerToken);

    private async Task<PublishedListing> PublishedListingAsync()
    {
        Factory.EnsureDatabaseCreated();

        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var seller = await HttpClient.PostAsJsonAsync(
            $"{Root}/sellers",
            new { displayName = "Case Loop Seller", countryCode = "MU", kind = "Individual" },
            JsonOptions);
        seller.StatusCode.Should().Be(HttpStatusCode.Created, await seller.Content.ReadAsStringAsync());

        var created = await HttpClient.PostAsJsonAsync(
            $"{Root}/listings",
            new
            {
                title = $"Racing bicycle {Guid.NewGuid():N}",
                description = "Carbon frame, one owner, recently serviced.",
                categoryKey = "bicycles",
                countryCode = "MU",
                listingType = "FixedPrice",
                condition = "Good",
                price = 9000,
                currency = "MUR",
                offersPickup = true,
                mediaIds = new[] { Guid.NewGuid() }
            },
            JsonOptions);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var listing = await ReadDataAsync<ListingShape>(created);

        var published = await HttpClient.PostAsJsonAsync(
            $"{Root}/listings/{listing.ListingId}/publish", new { activateImmediately = true }, JsonOptions);
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        return new PublishedListing(listing.ListingId, owner.Tokens.AccessToken);
    }

    private async Task ReportAsync(Guid listingId, string reason, string detail)
    {
        var (_, reporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporter.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/listings/{listingId}/report", new { reason, detail }, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpStatusCode> AnonymousStatusAsync(Guid listingId)
    {
        using var anonymous = Factory.CreateClient();

        return (await anonymous.GetAsync($"{Root}/listings/{listingId}")).StatusCode;
    }

    private async Task<ListingShape> StaffViewAsync(Guid listingId) =>
        await ReadDataAsync<ListingShape>(await HttpClient.GetAsync($"{Root}/listings/{listingId}"));

    /// <summary>
    /// The listing's report rows, read from the database: the module's queue is shared by the
    /// whole collection, and a first page of it is not this listing's.
    /// </summary>
    private async Task<List<string>> ReportStatusesAsync(Guid listingId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var id = MarketplaceListingId.From(listingId);

        var statuses = await dbContext.Set<ListingReport>()
            .AsNoTracking()
            .Where(report => report.ListingId == id)
            .Select(report => report.Status)
            .ToListAsync();

        return [.. statuses.Select(status => status.ToString())];
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

    private sealed record ListingShape(Guid ListingId, string? ModerationState);

    private sealed record DecisionShape(string? Kind, string? Outcome);

    private sealed record CaseShape(
        Guid ItemId,
        string? ReasonCode,
        int ReportCount,
        string? CurrentOutcome,
        string SecondReview,
        List<DecisionShape> Decisions);
}
