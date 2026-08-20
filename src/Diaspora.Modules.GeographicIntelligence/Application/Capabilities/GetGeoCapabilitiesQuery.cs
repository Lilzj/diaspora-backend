using Diaspora.Application.Abstractions;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Heat;
using Diaspora.Modules.GeographicIntelligence.Domain.Routing;
using Diaspora.Modules.GeographicIntelligence.Domain.Scores;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.GeographicIntelligence.Application.Capabilities;

/// <summary>What this deployment's geographic stack can actually do.</summary>
public sealed record GetGeoCapabilitiesQuery : IQuery<GeoCapabilitiesResponse>;

/// <summary>
/// Reports the truth about the providers, so a client stops inferring it from failures.
/// <para>
/// <b>Derived from the registered providers, never from configuration text.</b> The routing row
/// reads <see cref="IRoutingProvider.SupportedModes"/> off whatever is in the container, so a
/// deployment cannot advertise a capability by editing a setting — the only way this says routing
/// is available is for a provider that answers routes to be registered. That is the difference
/// between a capability report and a promise.
/// </para>
/// <para>
/// It publishes a provider <em>key</em> and nothing else. No endpoint, no credential, no quota and
/// no vendor account: §27 forbids exposing provider configuration and the simplest way to keep that
/// is for there to be nothing else on the record to expose.
/// </para>
/// </summary>
public sealed class GetGeoCapabilitiesQueryHandler
    : IQueryHandler<GetGeoCapabilitiesQuery, GeoCapabilitiesResponse>
{
    private readonly IRoutingProvider _routing;
    private readonly GeospatialProviderRegistry _spatial;

    public GetGeoCapabilitiesQueryHandler(
        IRoutingProvider routing, GeospatialProviderRegistry spatial)
    {
        _routing = routing;
        _spatial = spatial;
    }

    public Task<Result<GeoCapabilitiesResponse>> Handle(
        GetGeoCapabilitiesQuery request, CancellationToken cancellationToken)
    {
        var modes = _routing.SupportedModes;
        var routes = modes.Count > 0;

        var capabilities = new List<GeoCapabilityResponse>
        {
            new()
            {
                Capability = "neighborhood-scores",
                Available = true,
                Provider = "platform",
                Detail =
                    "Composed from moderated neighbourhood profiles and counts of published "
                    + "content. Which dimensions a particular place has depends on whether anybody "
                    + "has assessed it."
            },
            new()
            {
                Capability = "cost-of-living",
                Available = false,
                Provider = "none",
                Detail =
                    "Diaspora publishes one moderated relative band per neighbourhood. No index, "
                    + "currency, baseline or category breakdown exists, and no source is contracted."
            },
            new()
            {
                Capability = "safety-scores",
                Available = false,
                Provider = "none",
                Detail =
                    "Diaspora holds no safety data and no source is contracted for any country it "
                    + "operates in."
            },
            new()
            {
                Capability = "heat-layers",
                Available = true,
                Provider = "platform",
                Detail =
                    "Live counts of published content per catalogue place, suppressed below a "
                    + "configured floor."
            },
            new()
            {
                Capability = "bounding-box-search",
                Available = _spatial.Supports(GeospatialCapability.BoundingBoxSearch),
                Provider = _spatial.Active.Key,
                Detail = "Viewport search over the geographic catalogue."
            },
            new()
            {
                Capability = "routing",
                Available = routes,
                Provider = _routing.Key,
                Detail = routes
                    ? "A routing provider is configured."
                    : "No routing provider is configured. Travel times and directions are "
                      + "unavailable and no estimate is substituted."
            },
            new()
            {
                Capability = "transit-routing",
                Available = modes.Contains(TravelMode.Transit),
                Provider = _routing.Key,
                Detail = modes.Contains(TravelMode.Transit)
                    ? "The configured provider answers public transport itineraries."
                    : "No provider answers public transport. Diaspora holds no timetable, stop "
                      + "inventory or service calendar."
            },
            new()
            {
                Capability = "street-level-geocoding",
                Available = false,
                Provider = "catalog",
                Detail =
                    "Addresses resolve to Diaspora's own place catalogue — neighbourhoods and "
                    + "cities. Street addresses and points of interest need a vendor and none is "
                    + "configured."
            }
        };

        return Task.FromResult(Result.Success(new GeoCapabilitiesResponse
        {
            Capabilities = capabilities,
            SupportedTravelModes = [.. modes.Select(mode => mode.ToString())],
            SupportedHeatLayers = HeatLayerCatalog.SupportedLayers,

            // The dimensions this deployment could serve for some place, not for every place. A
            // neighbourhood nobody has assessed still has none of them, which is per-neighbourhood
            // and reported there.
            SupportedScoreDimensions =
            [
                ScoreDimensionId.Transport,
                ScoreDimensionId.CostOfLiving,
                ScoreDimensionId.Housing,
                ScoreDimensionId.CommunityActivity
            ]
        }));
    }
}
