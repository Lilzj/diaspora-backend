using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Enums;

namespace Diaspora.Modules.Location.Application.Search;

/// <summary>
/// Place-name autocomplete — the query the global location selector sends on every keystroke.
/// <para>
/// The one existing Location endpoint the frontend actually calls today, debounced to 300 ms.
/// It answers with <see cref="LocationReferenceResponse"/> rather than a search-specific DTO
/// because what the selector does with a result is set it as the current location, and a
/// result already in that shape saves the client a mapping step.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.Search)]
public sealed record SearchLocationsQuery : IQuery<LocationSearchResponse>
{
    public required string Term { get; init; }

    /// <summary>Restricts to one country, for a picker already scoped to one.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Restricts to these rungs. Empty means every rung.</summary>
    public IReadOnlyList<AdministrativeLevel> Levels { get; init; } = [];

    /// <summary>Biases ordering toward results near the member, when the client knows where.</summary>
    public double? NearLatitude { get; init; }

    public double? NearLongitude { get; init; }

    public int? Limit { get; init; }
}
