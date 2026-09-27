using Diaspora.Modules.Location.Domain.Places;

namespace Diaspora.Modules.Location.Infrastructure.Places;

/// <summary>
/// The deployment's answer when no place vendor is configured: "not configured", every time.
/// <para>
/// Reachable on purpose, for the reason <c>NotConfiguredRoutingProvider</c> gives: the client can
/// then say "this deployment has no address search" from the wire, rather than guessing.
/// </para>
/// </summary>
internal sealed class NotConfiguredPlaceSearchProvider : IPlaceSearchProvider
{
    public string Key => "none";

    public bool IsConfigured => false;

    public Task<PlaceSearchResult<IReadOnlyList<PlaceSuggestion>>> AutocompleteAsync(
        PlaceAutocompleteRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(PlaceSearchResult<IReadOnlyList<PlaceSuggestion>>.Failed(
            PlaceSearchOutcome.NotConfigured, Key));

    public Task<PlaceSearchResult<PlaceDetail>> GetDetailsAsync(
        PlaceDetailsRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(PlaceSearchResult<PlaceDetail>.Failed(PlaceSearchOutcome.NotConfigured, Key));
}
