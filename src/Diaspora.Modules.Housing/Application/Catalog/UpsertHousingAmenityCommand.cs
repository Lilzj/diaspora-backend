using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Catalog;

/// <summary>
/// Creates or revises an amenity.
/// <para>
/// The amenity catalogue is the one a deployment changes most: what counts as worth advertising is
/// local. A generator and a borehole matter in Lagos and mean nothing in Berlin; district heating
/// is the reverse. Shipping one list and calling it configurable would have been a lie.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.ManageCatalogue)]
[Auditable]
public sealed record UpsertHousingAmenityCommand : ICommand<HousingAmenityCatalogResponse>
{
    public required UpsertHousingAmenityRequest Request { get; init; }
}
