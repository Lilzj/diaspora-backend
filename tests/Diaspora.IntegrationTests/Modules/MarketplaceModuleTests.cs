using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Marketplace &amp; Commerce Platform (B10), driven over real HTTP through the same host every
/// other module suite uses.
/// <para>
/// Three claims are under test. That the module composed into the platform through nothing but
/// contributor interfaces — its feature gates turn up in Identity's catalogue, its name in the
/// platform manifest — with no edit to the kernel, to Identity, or to <c>Program.cs</c>. That the
/// gates it inherited from the host's provisional catalogue kept their names and tier floors,
/// because the mobile client already renders against them. And that the B10 rule which decides who
/// may do what actually holds: <b>looking is Limited, transacting is Verified.</b>
/// </para>
/// <para>
/// The wire shapes below are re-declared by hand rather than referenced from the module. Binding
/// the tests to B10's own response records would make them pass whenever the module compiles, which
/// is the one thing a contract test must never do.
/// </para>
/// </summary>
public sealed class MarketplaceModuleTests : IdentityTestBase
{
    private const string Root = "/api/v1/marketplace";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MarketplaceModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Proof the module's <c>IFeatureContributor</c> reached the composed catalogue: gates B10 owns
    /// turn up in Identity's response, which the Marketplace module never touched.
    /// </summary>
    [Fact]
    public async Task The_marketplace_feature_gates_composed_into_the_platform_catalogue()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var names = features.Features.Select(feature => feature.Name).ToList();

        names.Should().Contain(
            "marketplace.browse", "browsing is a gate the Marketplace module owns");

        names.Should().Contain(
            "marketplace.listings.create", "selling is the module's progression gate");

        names.Should().Contain("marketplace.save");
        names.Should().Contain("marketplace.offer");
        names.Should().Contain("marketplace.reserve");
        names.Should().Contain("marketplace.storefront");
    }

    /// <summary>
    /// <b>The regression this phase most had to avoid.</b> <c>marketplace.browse</c> and
    /// <c>marketplace.listings.create</c> were declared by the host's provisional catalogue from B2
    /// onwards. If both the host and the module declared them the feature catalogue would refuse to
    /// compose and every request that resolves it would fail — including phone verification, which
    /// is how this very test creates its member. Reaching this assertion at all proves the duplicate
    /// is gone; the assertion proves the names and floors survived the move.
    /// </summary>
    [Fact]
    public async Task The_gates_inherited_from_the_provisional_catalogue_kept_their_floors()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        features.Features.Where(feature => feature.Name == "marketplace.browse")
            .Should().ContainSingle("two modules declaring one gate would fail composition");

        features.Features.Where(feature => feature.Name == "marketplace.listings.create")
            .Should().ContainSingle();

        features.Features.Single(feature => feature.Name == "marketplace.browse")
            .IsEnabled.Should().BeTrue("browsing opens at Limited, as it always did");

        features.Features.Single(feature => feature.Name == "marketplace.listings.create")
            .IsEnabled.Should().BeFalse("selling is floored at Verified, as it always was");
    }

    [Fact]
    public async Task The_selling_gate_opens_for_a_verified_member()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        features.Features.Single(feature => feature.Name == "marketplace.listings.create")
            .IsEnabled.Should().BeTrue(
                "a Verified member sits exactly on the selling floor");
    }

    [Fact]
    public async Task The_platform_manifest_lists_the_marketplace_module()
    {
        var response = await HttpClient.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(
            "Marketplace",
            "the kernel discovered the module and published it in the manifest");
    }

    // ── The catalogues seeded ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The commerce taxonomy arrived through <c>IListingCategoryContributor</c>. Without it no
    /// listing could be drafted, because a category is required.
    /// </summary>
    [Fact]
    public async Task The_commerce_taxonomy_seeded_at_startup()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await IdentityApiClient.ReadDataAsync<List<CategoryShape>>(response);

        categories.Should().NotBeEmpty("the default contributor seeds the shipped taxonomy");

        categories.Select(category => category.Key)
            .Should().Contain(
                ["electronics", "furniture", "vehicles", "baby-products", "food"],
                "the shipped taxonomy leads with what diaspora members actually trade in");
    }

    /// <summary>
    /// The tree is hierarchical, and the materialised path is what makes a subtree query a prefix
    /// match rather than a recursive descent.
    /// </summary>
    [Fact]
    public async Task The_taxonomy_is_hierarchical_and_carries_materialised_paths()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var categories = await IdentityApiClient.ReadDataAsync<List<CategoryShape>>(
            await HttpClient.GetAsync($"{Root}/categories"));

        var phones = categories.Single(category => category.Key == "mobile-phones");

        phones.ParentKey.Should().Be("electronics");
        phones.Path.Should().Be("/electronics/mobile-phones/");
        phones.Depth.Should().Be(2);
    }

    /// <summary>
    /// The attribute catalogue arrived through <c>IListingAttributeContributor</c>, and it is what
    /// the create form is generated from. Definitions scoped to an ancestor apply to the leaf —
    /// which is the whole reason they are scoped by path.
    /// </summary>
    [Fact]
    public async Task A_category_reports_the_attributes_its_ancestors_declared()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync(
            $"{Root}/categories/mobile-phones/attributes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var attributes = await IdentityApiClient.ReadDataAsync<List<AttributeShape>>(response);

        attributes.Select(attribute => attribute.Key)
            .Should().Contain("storage-gb", "declared on the phones leaf itself");

        attributes.Select(attribute => attribute.Key)
            .Should().Contain("brand", "declared on /electronics/ and inherited by every leaf");
    }

    /// <summary>
    /// A choice attribute carries its options, so a client can render a picker without a second
    /// call — and the stored value is stable while the label is free to be translated.
    /// </summary>
    [Fact]
    public async Task A_choice_attribute_carries_its_options()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var attributes = await IdentityApiClient.ReadDataAsync<List<AttributeShape>>(
            await HttpClient.GetAsync($"{Root}/categories/cars/attributes"));

        var fuel = attributes.Single(attribute => attribute.Key == "fuel-type");

        fuel.ValueKind.Should().Be("Choice");
        fuel.Choices.Select(choice => choice.Value)
            .Should().Contain(["petrol", "diesel", "hybrid", "electric"]);
    }

    // ── Discovery is open ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The home surface answers an anonymous caller.</b> Somebody deciding whether this platform
    /// is worth joining has to be able to see what is for sale on it, and a marketplace whose front
    /// door required an account would be one nobody walks through.
    /// </summary>
    [Fact]
    public async Task The_marketplace_home_surface_answers_without_a_token()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync(Root);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dashboard = await IdentityApiClient.ReadDataAsync<DashboardShape>(response);

        dashboard.Seller.Should().BeNull("an anonymous reader has no seller profile");
        dashboard.TopCategories.Should().NotBeEmpty("the browse tiles are the point of the screen");
        dashboard.SavedListings.Should().Be(0);
    }

    [Fact]
    public async Task Search_answers_without_a_token()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/search", new { text = "washing machine" }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await IdentityApiClient.ReadDataAsync<SearchShape>(response);

        results.Items.Should().NotBeNull();
    }

    // ── Wire shapes, re-declared by hand ────────────────────────────────────────────────────

    private sealed record CategoryShape
    {
        public string Key { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string? ParentKey { get; init; }

        public string Path { get; init; } = string.Empty;

        public int Depth { get; init; }

        public bool IsActive { get; init; }
    }

    private sealed record AttributeShape
    {
        public string Key { get; init; } = string.Empty;

        public string Label { get; init; } = string.Empty;

        public string ValueKind { get; init; } = string.Empty;

        public bool IsRequired { get; init; }

        public List<ChoiceShape> Choices { get; init; } = [];
    }

    private sealed record ChoiceShape
    {
        public string Value { get; init; } = string.Empty;

        public string Label { get; init; } = string.Empty;
    }

    private sealed record DashboardShape
    {
        public object? Seller { get; init; }

        public int SavedListings { get; init; }

        public List<CategoryShape> TopCategories { get; init; } = [];
    }

    private sealed record SearchShape
    {
        public List<object> Items { get; init; } = [];

        public bool HasMore { get; init; }
    }
}
