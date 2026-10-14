using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Location;

/// <summary>
/// The seams that let a vertical contribute to Location without Location knowing it exists.
/// <para>
/// These are the tests that matter most to the phases after this one. If the registries stop
/// failing on a duplicate resource type, two modules will silently overwrite each other's
/// results differently on each deploy — and the bug report will say "housing results are
/// sometimes missing", which is close to undiagnosable.
/// </para>
/// </summary>
public sealed class LocationExtensibilityTests
{
    // ── Nearby providers ─────────────────────────────────────────────────

    [Fact]
    public void Two_providers_claiming_one_resource_type_fail_at_construction()
    {
        var act = () => new NearbyResourceProviderRegistry(
        [
            new StubProvider("housing"),
            new StubProvider("housing")
        ]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*housing*owned by exactly one module*");
    }

    /// <summary>Case-insensitively, because a collision spelled differently is still a collision.</summary>
    [Fact]
    public void A_resource_type_collision_is_detected_regardless_of_case()
    {
        var act = () => new NearbyResourceProviderRegistry(
        [
            new StubProvider("Housing"),
            new StubProvider("housing")
        ]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_provider_with_no_resource_type_fails_at_construction()
    {
        var act = () => new NearbyResourceProviderRegistry([new StubProvider("  ")]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void An_empty_registry_is_valid()
    {
        var registry = new NearbyResourceProviderRegistry([]);

        registry.Count.Should().Be(0);
        registry.ResourceTypes.Should().BeEmpty();
    }

    /// <summary>
    /// The response shape has to be stable across restarts, so the fan-out order is
    /// deterministic rather than assembly-load order.
    /// </summary>
    [Fact]
    public void Providers_are_returned_in_a_deterministic_order()
    {
        var registry = new NearbyResourceProviderRegistry(
        [
            new StubProvider("marketplace"),
            new StubProvider("housing"),
            new StubProvider("jobs")
        ]);

        registry.ResourceTypes.Should().ContainInOrder("housing", "jobs", "marketplace");
    }

    /// <summary>A provider can opt out cheaply rather than being queried and returning nothing.</summary>
    [Fact]
    public void A_provider_that_does_not_apply_is_not_queried()
    {
        var registry = new NearbyResourceProviderRegistry(
        [
            new StubProvider("housing", appliesTo: false),
            new StubProvider("jobs")
        ]);

        var applicable = registry.ProvidersFor(SampleRequest());

        applicable.Should().ContainSingle();
        applicable[0].ResourceType.Should().Be("jobs");
    }

    // ── Resource counters ────────────────────────────────────────────────

    [Fact]
    public void Two_counters_claiming_one_resource_type_fail_at_construction()
    {
        var act = () => new LocationResourceCounterRegistry(
        [
            new StubCounter("jobs"),
            new StubCounter("jobs")
        ]);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Country metadata ─────────────────────────────────────────────────

    /// <summary>
    /// Last wins, so overriding requires registering a higher-ordered contributor rather than
    /// removing a lower one.
    /// </summary>
    [Fact]
    public void A_later_metadata_contributor_overrides_an_earlier_one()
    {
        var catalog = new CountryMetadataCatalog(
        [
            new StubMetadataContributor(order: 0, "GBP"),
            new StubMetadataContributor(order: 10, "EUR")
        ]);

        catalog.MetadataFor(CountryCode.Create("GB")).Currency!.Value.Should().Be("EUR");
    }

    /// <summary>
    /// Null means "no opinion". An empty record would override and erase what a lower-ordered
    /// contributor supplied.
    /// </summary>
    [Fact]
    public void A_contributor_returning_null_does_not_erase_an_earlier_answer()
    {
        var catalog = new CountryMetadataCatalog(
        [
            new StubMetadataContributor(order: 0, "GBP"),
            new StubMetadataContributor(order: 10, currency: null)
        ]);

        catalog.MetadataFor(CountryCode.Create("GB")).Currency!.Value.Should().Be("GBP");
    }

    [Fact]
    public void A_country_nobody_knows_resolves_to_empty_metadata()
    {
        var catalog = new CountryMetadataCatalog([]);

        catalog.MetadataFor(CountryCode.Create("ZZ")).Currency.Should().BeNull();
    }

    private static NearbyResourceRequest SampleRequest() =>
        new()
        {
            Search = NearbySearch.Create(
                GeoCoordinate.Create(51.4742d, -0.0691d),
                SearchRadius.Default,
                new HaversineDistanceCalculator()).Value,
            CountryCode = CountryCode.Create("GB"),
            Limit = 10
        };

    private sealed class StubProvider : INearbyResourceProvider
    {
        private readonly bool _appliesTo;

        public StubProvider(string resourceType, bool appliesTo = true)
        {
            ResourceType = resourceType;
            _appliesTo = appliesTo;
        }

        public string ResourceType { get; }

        public bool AppliesTo(NearbyResourceRequest request) => _appliesTo;

        public Task<NearbyResourceGroup> FindNearbyAsync(
            NearbyResourceRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(NearbyResourceGroup.Empty(ResourceType));
    }

    private sealed class StubCounter : ILocationResourceCounter
    {
        public StubCounter(string resourceType) => ResourceType = resourceType;

        public string ResourceType { get; }

        public Task<int> CountInAreaAsync(
            AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<int> CountInCountryAsync(
            CountryCode countryCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<MapPointPage> ListPointsAsync(
            MapPointQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(MapPointPage.Empty);
    }

    private sealed class StubMetadataContributor : ICountryMetadataContributor
    {
        private readonly string? _currency;

        public StubMetadataContributor(int order, string? currency)
        {
            Order = order;
            _currency = currency;
        }

        public int Order { get; }

        public CountryMetadata? MetadataFor(CountryCode countryCode) =>
            _currency is null
                ? null
                : CountryMetadata.Create(
                    currency: CurrencyCode.Create(_currency));
    }
}
