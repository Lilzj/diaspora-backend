using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Provider &amp; Service Ecosystem Engine, over real HTTP through the same host every other
/// suite uses.
/// <para>
/// Three things are under test at once. The module's behaviour. That a module with twenty-one
/// tables, fifteen permissions and its own route prefix composed into the platform through
/// nothing but contributor interfaces and one project reference — no <c>Program.cs</c> change, no
/// kernel change, no edit to Identity, Trust, Location, Media or Community. And the thing that
/// makes B7 the foundation the brief asks for: that a restaurant, a church and a freelance
/// consultant all come back from the same endpoint in the same shape.
/// </para>
/// </summary>
public sealed class ProvidersModuleTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProvidersModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_provider_feature_gates_composed_into_the_platform_catalogue()
    {
        // Proof the module's IFeatureContributor reached the composed catalogue: gates it owns
        // turn up in Identity's response, which the Providers module never touched.
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var names = features.Features.Select(feature => feature.Name).ToList();

        names.Should().Contain("providers.discovery");
        names.Should().Contain("providers.registration");
        names.Should().Contain("providers.reviews");
    }

    [Fact]
    public async Task The_platform_manifest_lists_the_providers_module()
    {
        var response = await HttpClient.GetAsync("/");

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Providers");
    }

    [Fact]
    public async Task The_capabilities_endpoint_reports_what_this_deployment_offers()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await HttpClient.GetAsync("/api/v1/providers/capabilities"));

        // Read from the registries, not from constants — a deployment that contributes a
        // provider type or a ranking strategy says so without an app release.
        capabilities.ProviderTypes.Select(type => type.Type)
            .Should().Contain(["Individual", "Business", "Church", "GovernmentService"]);

        capabilities.RankingKeys.Should().Contain("balanced");
        capabilities.SortOrders.Should().Contain("Recommended");

        // The client's own review cap, confirmed by the server rather than hard-coded twice.
        capabilities.MaxReviewBodyLength.Should().Be(500);
    }

    // ── The seeded catalogue, and the alias migration path ──────────────────────────────────

    [Fact]
    public async Task The_service_category_catalogue_is_seeded_and_hierarchical()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var categories = await ReadDataAsync<IReadOnlyList<CategoryDto>>(
            await HttpClient.GetAsync("/api/v1/providers/categories"));

        categories.Should().NotBeEmpty();

        var food = categories.Should().ContainSingle(
            category => category.Slug == "food-restaurants").Subject;

        food.Depth.Should().Be(1);
        food.ParentId.Should().BeNull();

        // The frontend's three vocabularies merged: 'Restaurants' was an OrgCategory member and
        // is now a child of the mockCategories root it collided with.
        var restaurants = categories.Should().ContainSingle(
            category => category.Slug == "restaurants").Subject;

        restaurants.ParentId.Should().Be(food.CategoryId);
        restaurants.Depth.Should().Be(2);
        restaurants.Path.Should().Be("/food-restaurants/restaurants/");
    }

    [Fact]
    public async Task Registration_resolves_a_legacy_client_category_string_through_an_alias()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // 'Restaurants' is the frontend's OrgCategory member, not a slug and not a name the
        // catalogue's root uses. It resolves because it was registered as an alias — which is
        // what lets a client that has not shipped a release keep working.
        var provider = await RegisterProviderAsync("Mama Afrika Kitchen", category: "Restaurants");

        provider.Category.Slug.Should().Be("restaurants");
    }

    // ── Registration and the lifecycle ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_registered_provider_is_a_draft_and_is_not_discoverable()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var provider = await RegisterProviderAsync("Ebony Braids & Beauty", "Hair & Beauty");

        provider.Status.Should().Be("Draft");
        provider.PublishedAt.Should().BeNull();

        // A draft is invisible in discovery even to its own owner's search — publishing is an
        // explicit act, so nobody discovers a half-written listing of theirs already live.
        var page = await ReadDataAsync<ProviderPageDto>(
            await HttpClient.GetAsync("/api/v1/providers?query=Ebony"));

        page.Items.Should().NotContain(item => item.ProviderId == provider.ProviderId);
    }

    [Fact]
    public async Task Publishing_makes_a_provider_discoverable()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var provider = await RegisterProviderAsync("Naija Express Money Transfer", "Money Transfer");

        var published = await PublishAsync(provider.ProviderId);

        published.Status.Should().Be("Active");
        published.PublishedAt.Should().NotBeNull();

        var page = await ReadDataAsync<ProviderPageDto>(
            await HttpClient.GetAsync("/api/v1/providers?query=Naija"));

        page.Items.Should().ContainSingle(
            item => item.ProviderId == provider.ProviderId);
    }

    [Fact]
    public async Task A_provider_with_no_way_to_be_contacted_cannot_be_published()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // A listing a member finds, wants, and has nothing to tap on is worse than no listing.
        var provider = await RegisterProviderAsync(
            "Silent Trader", "Retail & Shopping", includeContact: false);

        var response = await HttpClient.PutAsJsonAsync(
            $"/api/v1/providers/{provider.ProviderId}/status",
            new { status = "Active" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadAsStringAsync();
        problem.Should().Contain("providers.provider.not-publishable");
    }

    [Fact]
    public async Task The_deep_link_slug_resolves_to_the_same_provider()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var provider = await RegisterProviderAsync("Peckham Plumbing Co", "Trades & Repairs");
        await PublishAsync(provider.ProviderId);

        var bySlug = await ReadDataAsync<ProviderDto>(
            await HttpClient.GetAsync($"/api/v1/providers/slug/{provider.Slug}"));

        bySlug.ProviderId.Should().Be(provider.ProviderId);
        bySlug.Slug.Should().Be("peckham-plumbing-co");
    }

    [Fact]
    public async Task Two_providers_with_the_same_name_get_distinct_slugs()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var first = await RegisterProviderAsync("Mama's Kitchen", "Restaurants");
        var second = await RegisterProviderAsync("Mama's Kitchen", "Restaurants");

        // Two restaurants genuinely called the same thing is not an error, and neither of them
        // should lose its deep link to the other.
        first.Slug.Should().NotBe(second.Slug);
        second.Slug.Should().StartWith("mamas-kitchen");
    }

    // ── One contract for every kind of provider ─────────────────────────────────────────────

    [Fact]
    public async Task A_church_and_a_restaurant_come_back_in_the_same_shape()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var church = await RegisterProviderAsync(
            "Redeemed Assembly Peckham", "Churches", providerType: "Church");

        var restaurant = await RegisterProviderAsync(
            "Accra Grill", "Restaurants", providerType: "Business");

        // The whole architectural claim of B7, asserted: no second model, no second endpoint,
        // and the blocks that do not apply are simply absent.
        church.ProviderType.Should().Be("Church");
        restaurant.ProviderType.Should().Be("Business");

        church.Availability.Should().NotBeNull();
        church.Verification.Should().NotBeNull();
        church.ReviewSummary.TotalReviews.Should().Be(0);

        // An individual and a church have no company number, and the block is absent rather
        // than present-and-empty.
        church.Organization.Should().BeNull();
        restaurant.Organization.Should().BeNull();
    }

    [Fact]
    public async Task An_individual_provider_needs_no_organization_detail()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var braider = await RegisterProviderAsync(
            "Fatima Braids", "Hair Braiding", providerType: "Individual");

        braider.ProviderType.Should().Be("Individual");
        braider.Organization.Should().BeNull();
        braider.Verification.IsVerified.Should().BeFalse();
    }

    // ── Reviews ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_review_updates_the_providers_rating_rollup()
    {
        var (_, ownerAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(ownerAuth.Tokens.AccessToken);

        var provider = await RegisterProviderAsync("Brixton Barbers", "Barbers");
        await PublishAsync(provider.ProviderId);

        var (_, reviewerAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reviewerAuth.Tokens.AccessToken);

        var review = await ReadDataAsync<ReviewDto>(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/providers/{provider.ProviderId}/reviews",
                new { rating = 5, body = "Best fade in south London." }));

        review.Rating.Should().Be(5);

        var reread = await ReadDataAsync<ProviderDto>(
            await HttpClient.GetAsync($"/api/v1/providers/{provider.ProviderId}"));

        // Recomputed from the reviews table inside the same transaction, not incremented.
        reread.ReviewSummary.TotalReviews.Should().Be(1);
        reread.ReviewSummary.Average.Should().Be(5);
        reread.ReviewSummary.Distribution["5"].Should().Be(1);
    }

    [Fact]
    public async Task A_member_may_not_review_a_provider_they_are_attached_to()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var provider = await RegisterProviderAsync("Own Goal Cafe", "Restaurants");
        await PublishAsync(provider.ProviderId);

        // Self-reviews are the cheapest way to poison a directory's ratings.
        var response = await HttpClient.PostAsJsonAsync(
            $"/api/v1/providers/{provider.ProviderId}/reviews",
            new { rating = 5, body = "Excellent, obviously." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("providers.review.self");
    }

    [Fact]
    public async Task Reviewing_twice_revises_rather_than_failing()
    {
        var (_, ownerAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(ownerAuth.Tokens.AccessToken);

        var provider = await RegisterProviderAsync("Lewisham Launderette", "Cleaning");
        await PublishAsync(provider.ProviderId);

        var (_, reviewerAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reviewerAuth.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"/api/v1/providers/{provider.ProviderId}/reviews", new { rating = 5 });

        // A double tap, or a retry after a timeout, is the obvious thing to do — and a 409 for
        // it would be the server punishing a slow connection.
        var second = await HttpClient.PostAsJsonAsync(
            $"/api/v1/providers/{provider.ProviderId}/reviews",
            new { rating = 3, body = "On reflection." });

        second.IsSuccessStatusCode.Should().BeTrue();

        var page = await ReadDataAsync<ReviewPageDto>(
            await HttpClient.GetAsync($"/api/v1/providers/{provider.ProviderId}/reviews"));

        page.Items.Should().ContainSingle().Which.Rating.Should().Be(3);
        page.Summary!.TotalReviews.Should().Be(1);
        page.Summary.Average.Should().Be(3);
    }

    // ── Bookmarks, delegated to Community ───────────────────────────────────────────────────

    [Fact]
    public async Task Saving_a_provider_is_idempotent_and_stored_by_community()
    {
        var (_, ownerAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(ownerAuth.Tokens.AccessToken);

        var provider = await RegisterProviderAsync("Deptford Diner", "Restaurants");
        await PublishAsync(provider.ProviderId);

        var (_, memberAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(memberAuth.Tokens.AccessToken);

        // B7 publishes the endpoint; B6 owns the row. Deviation D-2.
        var first = await HttpClient.PutAsync(
            $"/api/v1/providers/{provider.ProviderId}/bookmark", null);

        var second = await HttpClient.PutAsync(
            $"/api/v1/providers/{provider.ProviderId}/bookmark", null);

        first.IsSuccessStatusCode.Should().BeTrue();
        second.IsSuccessStatusCode.Should().BeTrue();

        var reread = await ReadDataAsync<ProviderDto>(
            await HttpClient.GetAsync($"/api/v1/providers/{provider.ProviderId}"));

        reread.ViewerState!.IsBookmarked.Should().BeTrue();
    }

    // ── Discovery filters ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Choosing_a_parent_category_returns_providers_filed_under_its_children()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var provider = await RegisterProviderAsync("Camberwell Kitchen", "Restaurants");
        await PublishAsync(provider.ProviderId);

        // A member choosing "Food & Restaurants" expects the restaurants under it, not only the
        // providers filed against the parent node itself.
        var page = await ReadDataAsync<ProviderPageDto>(
            await HttpClient.GetAsync("/api/v1/providers?category=food-restaurants"));

        page.Items.Should().Contain(item => item.ProviderId == provider.ProviderId);
    }

    [Fact]
    public async Task Choosing_a_category_leaves_out_providers_filed_elsewhere()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var restaurant = await RegisterProviderAsync("Peckham Grill", "Restaurants");
        var store = await RegisterProviderAsync("Peckham Hardware", "General Store");
        await PublishAsync(restaurant.ProviderId);
        await PublishAsync(store.ProviderId);

        var page = await ReadDataAsync<ProviderPageDto>(
            await HttpClient.GetAsync("/api/v1/providers?category=food-restaurants&limit=100"));

        page.Items.Should().Contain(item => item.ProviderId == restaurant.ProviderId);
        page.Items.Should().NotContain(
            item => item.ProviderId == store.ProviderId,
            "a category is a filter, not a suggestion: the hardware shop is not food");
    }

    // ── Plain-language search (owner, 5 Nov 2026) ──────────────────────────────────────────
    //
    // "Where to braid my hair in Grand Baie" should find Zoe Hair Saloon. The text used to be
    // one case-sensitive phrase, so a sentence matched nothing; it now matches any word that
    // says something, in any case, and a word naming a category (its name or the keywords it
    // is known by) finds the providers filed under it.

    [Fact]
    public async Task A_sentence_finds_a_provider_by_any_of_its_words_in_any_case()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // Filed under the wrong category on purpose, as on production: the name still finds it.
        var salon = await RegisterProviderAsync("Zoe Hair Saloon", "General Store");
        var shop = await RegisterProviderAsync("Brixton Paint Shop", "General Store");
        await PublishAsync(salon.ProviderId);
        await PublishAsync(shop.ProviderId);

        var page = await ReadDataAsync<ProviderPageDto>(
            await HttpClient.GetAsync("/api/v1/providers?query=where%20to%20braid%20my%20HAIR&limit=100"));

        page.Items.Should().Contain(item => item.ProviderId == salon.ProviderId);
        page.Items.Should().NotContain(
            item => item.ProviderId == shop.ProviderId, "no word of the sentence is about paint");
    }

    [Fact]
    public async Task A_word_finds_the_providers_filed_under_the_category_it_names()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // Nothing in the name says braid; the category is known by "box braids".
        var braider = await RegisterProviderAsync("Ama Studio", "Hair Braiding");
        await PublishAsync(braider.ProviderId);

        var page = await ReadDataAsync<ProviderPageDto>(
            await HttpClient.GetAsync("/api/v1/providers?query=braid&limit=100"));

        page.Items.Should().Contain(item => item.ProviderId == braider.ProviderId);
    }

    [Fact]
    public async Task The_verified_filter_excludes_providers_holding_no_current_verification()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var provider = await RegisterProviderAsync("Unverified Wares", "General Store");
        await PublishAsync(provider.ProviderId);

        // Nothing in B7 can award itself a badge — only a Trust event can — so a freshly
        // published provider must not pass this filter.
        var page = await ReadDataAsync<ProviderPageDto>(
            await HttpClient.GetAsync("/api/v1/providers?verifiedOnly=true&query=Unverified"));

        page.Items.Should().NotContain(item => item.ProviderId == provider.ProviderId);
    }

    [Fact]
    public async Task The_clients_existing_sort_values_are_accepted_unchanged()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // discover.tsx sends 'default', 'distance' and 'rating'.
        foreach (var sort in new[] { "default", "distance", "rating" })
        {
            var response = await HttpClient.GetAsync($"/api/v1/providers?sort={sort}");

            response.IsSuccessStatusCode.Should().BeTrue(
                "the client's existing sort value '{0}' must keep working", sort);
        }
    }

    [Fact]
    public async Task An_unknown_sort_value_is_a_validation_failure_rather_than_a_silent_default()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/providers?sort=cheapest");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("providers.sort.unknown");
    }

    // ── Availability ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_provider_with_no_hours_recorded_reports_open_now_as_null()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var provider = await RegisterProviderAsync("Hours Unknown Ltd", "General Store");
        await PublishAsync(provider.ProviderId);

        var reread = await ReadDataAsync<ProviderDto>(
            await HttpClient.GetAsync($"/api/v1/providers/{provider.ProviderId}"));

        // Nullable all the way to the wire. The client's boolean has to render "Closed" here,
        // which is wrong in the direction that costs the provider business.
        reread.Availability.IsOpenNow.Should().BeNull();

        // Seven days always come back, so a client rendering a week of rows never has to
        // reconcile a sparse list against a calendar.
        reread.Availability.OperatingHours.Should().HaveCount(7);
        reread.Availability.OperatingHours.Should().OnlyContain(day => !day.IsSpecified);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<ProviderDto> RegisterProviderAsync(
        string name,
        string category,
        string providerType = "Business",
        bool includeContact = true)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["providerType"] = providerType,
            ["category"] = category,
            ["cityName"] = "London",
            ["countryCode"] = "GB"
        };

        if (includeContact)
        {
            payload["phone"] = "+447700900456";
        }

        var response = await HttpClient.PostAsJsonAsync("/api/v1/providers", payload);

        return await ReadDataAsync<ProviderDto>(response);
    }

    private async Task<ProviderDto> PublishAsync(Guid providerId)
    {
        var response = await HttpClient.PutAsJsonAsync(
            $"/api/v1/providers/{providerId}/status", new { status = "Active" });

        return await ReadDataAsync<ProviderDto>(response);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "the request should have succeeded but returned {0}: {1}",
            response.StatusCode,
            body);

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    // ── Wire shapes, deliberately re-declared rather than referenced ─────────────────────────
    //
    // These mirror the published contract by hand. Referencing B7's own response records would
    // make the test pass whenever the module compiles, which is exactly the thing a contract
    // test must not do: a rename would move both sides together and nobody would notice the
    // client had broken.

    private sealed record ProviderDto(
        Guid ProviderId,
        string Slug,
        string Name,
        string ProviderType,
        string Status,
        CategoryRefDto Category,
        OrganizationDto? Organization,
        AvailabilityDto Availability,
        VerificationDto Verification,
        ReviewSummaryDto ReviewSummary,
        ViewerStateDto? ViewerState,
        DateTimeOffset? PublishedAt);

    private sealed record CategoryRefDto(Guid CategoryId, string Slug, string Name);

    private sealed record OrganizationDto(string? RegistrationNumber);

    private sealed record AvailabilityDto(
        string Kind,
        string TimeZoneId,
        bool? IsOpenNow,
        IReadOnlyList<OperatingHoursDto> OperatingHours);

    private sealed record OperatingHoursDto(
        string DayOfWeek, bool IsClosed, bool IsSpecified, string DisplayText);

    private sealed record VerificationDto(bool IsVerified, string Status);

    private sealed record ReviewSummaryDto(
        double Average, int TotalReviews, IReadOnlyDictionary<string, int> Distribution);

    private sealed record ViewerStateDto(
        string? Role, bool CanReview, bool IsBookmarked, bool HasRecommended);

    private sealed record ProviderPageDto(
        IReadOnlyList<ProviderSummaryDto> Items, string? NextCursor, bool HasMore);

    private sealed record ProviderSummaryDto(
        Guid ProviderId, string Slug, string Name, bool IsVerified, bool? IsOpenNow);

    private sealed record ReviewDto(Guid ReviewId, int Rating, string? Body);

    private sealed record ReviewPageDto(
        IReadOnlyList<ReviewDto> Items, ReviewSummaryDto? Summary);

    private sealed record CategoryDto(
        Guid CategoryId, string Slug, string Name, Guid? ParentId, string Path, int Depth);

    private sealed record CapabilitiesDto(
        IReadOnlyList<ProviderTypeOptionDto> ProviderTypes,
        IReadOnlyList<string> RankingKeys,
        IReadOnlyList<string> SortOrders,
        int MaxReviewBodyLength);

    private sealed record ProviderTypeOptionDto(string Type, string Label);
}
