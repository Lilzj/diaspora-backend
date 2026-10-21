using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// 21 Oct 2026 — two triggers that spoke to everybody: <c>guide.savedChanged</c> and
/// <c>savedSearch.priceChanged</c> passed an empty owner set, which the engine reads as "anybody
/// watching", so a draft guide's edit and a draft listing's price reached strangers' automations.
/// <para>
/// Both sides of every rule, read as each member (run history is owner-filtered): the member the
/// fact concerns gets a run, and a stranger with the same automation does not. Every list read is
/// narrowed to one automation's runs, and every saved search names a value unique to its test.
/// </para>
/// </summary>
[Collection(SignalsCollection.Name)]
public sealed class AutomationAudienceTests : PooledIdentityTestBase
{
    private const string Automations = "/api/v1/automations";
    private const string Guides = "/api/v1/guides";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AutomationAudienceTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // guide.savedChanged
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A draft edit runs nobody's automation; once published, an edit runs the saver's and not a
    /// stranger's — over real HTTP, the real Bookmarks rows and the real outbox.
    /// </summary>
    [Fact]
    public async Task A_guide_edit_reaches_its_savers_once_published_and_nobody_while_a_draft()
    {
        var (_, saverToken) = await NewMemberAsync();
        var saverRule = await CreateAutomationAsync("A guide I saved changed", "guide.savedChanged");

        var (_, strangerToken) = await NewMemberAsync();
        var strangerRule = await CreateAutomationAsync("Any guide changed", "guide.savedChanged");

        var (_, authorToken) = await NewMemberAsync();
        var title = $"Renewing a residence permit {Guid.NewGuid():N}";
        var guideId = await CreateGuideAsync(title);

        Api.Authenticate(saverToken);
        var saved = await HttpClient.PostAsJsonAsync(
            "/api/v1/bookmarks", new { targetType = "Guide", targetId = guideId.ToString("D") }, JsonOptions);
        saved.IsSuccessStatusCode.Should().BeTrue(await saved.Content.ReadAsStringAsync());

        // ── The draft: edited, and nobody but its author may read it ──────────────────────────
        Api.Authenticate(authorToken);
        await UpdateGuideAsync(guideId, title, "Take the form, the fee and one hour of patience.");
        await Factory.DrainOutboxAsync();

        Api.Authenticate(saverToken);
        (await RunsForAsync(saverRule)).Items.Should().BeEmpty(
            "a draft's edit is its author's business, even to somebody who saved it");

        Api.Authenticate(strangerToken);
        (await RunsForAsync(strangerRule)).Items.Should().BeEmpty("nor anybody else's");

        // ── Published, then edited ────────────────────────────────────────────────────────────
        Api.Authenticate(authorToken);
        await PublishGuideAsync(guideId);
        await Factory.DrainOutboxAsync();

        // A title edit alone: one change, one contract, so one run is what "only the saver" looks like.
        await UpdateGuideAsync(guideId, $"{title} (2026)", body: null);
        await Factory.DrainOutboxAsync();

        Api.Authenticate(saverToken);
        var saverRun = (await RunsForAsync(saverRule)).Items.Should().ContainSingle(
            "the member who saved the guide is who \"a guide you saved\" means").Subject;
        saverRun.TriggerSummary.Should().Be("A guide you saved was updated.");

        Api.Authenticate(strangerToken);
        (await RunsForAsync(strangerRule)).Items.Should().BeEmpty(
            "a member who never saved the guide is not told about it");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // savedSearch.priceChanged — marketplace
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A real draft listing's price edit: the contract says it is not public, and the member whose
    /// saved search matches it hears nothing.
    /// </summary>
    [Fact]
    public async Task A_draft_listings_price_change_reaches_nobody()
    {
        var (_, watcherToken) = await NewMemberAsync();
        await SaveSearchAsync("marketplace", new { category = "bicycles" });
        var watcherRule = await CreateAutomationAsync("Bicycle prices", "savedSearch.priceChanged");

        await NewMemberAsync();

        var seller = await HttpClient.PostAsJsonAsync(
            "/api/v1/marketplace/sellers",
            new { displayName = "Draft Seller", countryCode = "MU", kind = "Individual" },
            JsonOptions);
        seller.StatusCode.Should().Be(HttpStatusCode.Created, await seller.Content.ReadAsStringAsync());

        var title = $"Child's bicycle {Guid.NewGuid():N}";

        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/marketplace/listings",
            new
            {
                title,
                description = "Outgrown, rides well. Collection from Grand Baie.",
                categoryKey = "bicycles",
                countryCode = "MU",
                cityName = "Grand Baie",
                condition = "Good",
                price = 500m,
                currency = "MUR",
                offersPickup = true
            },
            JsonOptions));

        var listingId = created.GetProperty("listingId").GetGuid();

        await DataAsync(await HttpClient.PutAsJsonAsync(
            $"/api/v1/marketplace/listings/{listingId}",
            new
            {
                title,
                description = "Outgrown, rides well. Collection from Grand Baie.",
                condition = "Good",
                price = 420m,
                currency = "MUR",
                countryCode = "MU",
                cityName = "Grand Baie",
                offersPickup = true
            },
            JsonOptions));

        var payload = await PendingPayloadAsync(
            "Diaspora.Contracts.Marketplace.V1.Events.ListingUpdatedIntegrationEvent", listingId);

        payload.Should().NotBeNull("the price edit must have reached the outbox");
        payload!["ChangedFields"]!.AsArray().Select(field => field!.GetValue<string>())
            .Should().Contain("price");
        payload["IsListedPublicly"]!.GetValue<bool>().Should().BeFalse("a draft is not public");
        payload["CategoryKey"]!.GetValue<string>().Should().Be("bicycles", "the search would match it");

        await Factory.DrainOutboxAsync();

        Api.Authenticate(watcherToken);
        (await RunsForAsync(watcherRule)).Items.Should().BeEmpty(
            "a draft's price is the seller's business, whoever's search it would match");
    }

    /// <summary>
    /// The listing's audience and the saved search, both sides of each: not public reaches nobody;
    /// public reaches the member whose search it matches, and neither the member whose search it does
    /// not match nor one with no search at all.
    /// </summary>
    [Fact]
    public async Task A_public_listings_price_change_reaches_only_the_matching_saved_search()
    {
        var category = $"price-audience-{Guid.NewGuid():N}";

        var (_, watcherToken) = await NewMemberAsync();
        await SaveSearchAsync("marketplace", new { category });
        var watcherRule = await CreateAutomationAsync("Watching", "savedSearch.priceChanged");

        var (_, otherToken) = await NewMemberAsync();
        await SaveSearchAsync("marketplace", new { category = $"{category}-elsewhere" });
        var otherRule = await CreateAutomationAsync("Watching something else", "savedSearch.priceChanged");

        var (_, strangerToken) = await NewMemberAsync();
        var strangerRule = await CreateAutomationAsync("No search at all", "savedSearch.priceChanged");

        var listingId = Guid.NewGuid();

        await PublishAsync(PriceChange(listingId, category, isListedPublicly: false));
        await Factory.DrainOutboxAsync();

        Api.Authenticate(watcherToken);
        (await RunsForAsync(watcherRule)).Items.Should().BeEmpty(
            "a listing a stranger could not find reaches nobody — and a message without the flag fails closed");

        await PublishAsync(PriceChange(listingId, category, isListedPublicly: true));
        await Factory.DrainOutboxAsync();

        Api.Authenticate(watcherToken);
        (await RunsForAsync(watcherRule)).Items.Should().ContainSingle(
            "the listing matches this member's saved search");

        Api.Authenticate(otherToken);
        (await RunsForAsync(otherRule)).Items.Should().BeEmpty(
            "a saved search the listing does not match is not watching it");

        Api.Authenticate(strangerToken);
        (await RunsForAsync(strangerRule)).Items.Should().BeEmpty(
            "an automation with no saved search is watching nothing");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // savedSearch.priceChanged — housing
    // ════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_public_propertys_rent_change_reaches_only_the_matching_saved_search()
    {
        var accommodationType = $"rent-audience-{Guid.NewGuid():N}";

        var (_, watcherToken) = await NewMemberAsync();
        await SaveSearchAsync("housing", new { accommodationType });
        var watcherRule = await CreateAutomationAsync("Watching rents", "savedSearch.priceChanged");

        var (_, otherToken) = await NewMemberAsync();
        await SaveSearchAsync("housing", new { accommodationType = $"{accommodationType}-elsewhere" });
        var otherRule = await CreateAutomationAsync("Watching other rents", "savedSearch.priceChanged");

        var listingId = Guid.NewGuid();

        await PublishAsync(RentChange(listingId, accommodationType, isListedPublicly: false));
        await Factory.DrainOutboxAsync();

        Api.Authenticate(watcherToken);
        (await RunsForAsync(watcherRule)).Items.Should().BeEmpty(
            "a draft or hidden property's rent is the landlord's business");

        await PublishAsync(RentChange(listingId, accommodationType, isListedPublicly: true));
        await Factory.DrainOutboxAsync();

        Api.Authenticate(watcherToken);
        (await RunsForAsync(watcherRule)).Items.Should().ContainSingle(
            "the property matches this member's saved search");

        Api.Authenticate(otherToken);
        (await RunsForAsync(otherRule)).Items.Should().BeEmpty(
            "a saved search the property does not match is not watching it");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static ListingUpdatedIntegrationEvent PriceChange(
        Guid listingId, string category, bool isListedPublicly) => new()
    {
        ListingId = listingId,
        SellerId = Guid.NewGuid(),
        ChangedFields = ["price"],
        PriceAmount = 420m,
        PreviousPriceAmount = 500m,
        CurrencyCode = "GBP",
        ChangedAt = DateTimeOffset.UtcNow,
        IsListedPublicly = isListedPublicly,
        IsPriceDisclosed = true,
        CategoryKey = category,
        CountryCode = "GB"
    };

    private static HousingUpdatedIntegrationEvent RentChange(
        Guid listingId, string accommodationType, bool isListedPublicly) => new()
    {
        ListingId = listingId,
        LandlordId = Guid.NewGuid(),
        ChangedFields = ["Price"],
        IsRentDisclosed = true,
        RentAmount = 1_150m,
        PreviousRentAmount = 1_200m,
        CurrencyCode = "GBP",
        CountryCode = "GB",
        ChangedAt = DateTimeOffset.UtcNow,
        IsListedPublicly = isListedPublicly,
        AccommodationTypeKey = accommodationType
    };

    /// <summary>A verified member, signed in, and the token to become them again.</summary>
    private async Task<(Guid Id, string Token)> NewMemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        return (auth.User.Id, auth.Tokens.AccessToken);
    }

    private async Task<string> CreateAutomationAsync(string name, string trigger)
    {
        var response = await HttpClient.PostAsJsonAsync(
            Automations,
            new
            {
                name,
                trigger = new { key = trigger },
                conditions = Array.Empty<object>(),
                actions = new[] { new { key = "notify.self" } },
                status = "ACTIVE",
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

        return (await DataAsync(response)).GetProperty("id").GetString()!;
    }

    private async Task SaveSearchAsync(string subject, object criteria)
    {
        var response = await HttpClient.PostAsJsonAsync(
            $"{Automations}/saved-searches",
            new
            {
                subject,
                name = $"Watching {Guid.NewGuid():N}",
                criteria = JsonSerializer.Serialize(criteria, JsonOptions),
                alertsEnabled = true,
                alertFrequency = "instant",
                watchFor = new[] { "newMatch" }
            },
            JsonOptions);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    private async Task<Guid> CreateGuideAsync(string title)
    {
        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            Guides,
            new { title, categoryKey = "immigration", language = "en", countryCode = "NG" },
            JsonOptions));

        return created.GetProperty("guideId").GetGuid();
    }

    private async Task UpdateGuideAsync(Guid guideId, string title, string? body) =>
        await DataAsync(await HttpClient.PutAsJsonAsync(
            $"{Guides}/{guideId}",
            new
            {
                title,
                countryCode = "NG",
                sections = body is null ? null : new[]
                {
                    new
                    {
                        sectionId = Guid.NewGuid(),
                        title = "What to do",
                        order = 0,
                        blocks = new[]
                        {
                            new { blockId = Guid.NewGuid(), kind = "Markdown", order = 0, content = body }
                        }
                    }
                }
            },
            JsonOptions));

    private async Task PublishGuideAsync(Guid guideId) =>
        await DataAsync(await HttpClient.PostAsJsonAsync(
            $"{Guides}/{guideId}/publish", new { revisionNotes = "First edition." }, JsonOptions));

    private async Task PublishAsync<T>(T integrationEvent)
        where T : Diaspora.Contracts.Events.IIntegrationEvent
    {
        using var scope = Factory.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<IIntegrationEventPublisher>()
            .PublishAsync(integrationEvent);

        await scope.ServiceProvider
            .GetRequiredService<Diaspora.Domain.Abstractions.IUnitOfWork>()
            .SaveChangesAsync();
    }

    /// <summary>The pending payload of one contract about one listing, parsed.</summary>
    private async Task<JsonObject?> PendingPayloadAsync(string eventType, Guid listingId)
    {
        var pending = await Factory.PendingOutboxAsync(eventType, limit: 500);

        return pending
            .Select(message => JsonNode.Parse(message.Payload)!.AsObject())
            .LastOrDefault(payload =>
                payload["ListingId"]?.GetValue<Guid>() == listingId);
    }

    private async Task<RunPage> RunsForAsync(string automationId)
    {
        var page = await DataAsync(
            await HttpClient.GetAsync($"{Automations}/runs?automationId={automationId}"));

        return new RunPage(
            [.. page.GetProperty("items").EnumerateArray().Select(item => new RunDto(
                item.GetProperty("triggerSummary").GetString() ?? string.Empty))]);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(body);

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Clone();
    }

    private sealed record RunPage(IReadOnlyList<RunDto> Items);

    private sealed record RunDto(string TriggerSummary);
}
