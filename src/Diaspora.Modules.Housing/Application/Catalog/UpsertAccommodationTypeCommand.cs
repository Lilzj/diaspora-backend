using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Catalog;

/// <summary>
/// Creates or revises an accommodation type.
/// <para>
/// <b>This is what "configurable" means in practice.</b> The brief asks for a configurable
/// accommodation-type catalogue, and a catalogue that can only be changed by shipping a new
/// contributor is configurable only to whoever can deploy. A deployment operating in a market where
/// "boys' quarters" or "self-contain" is what people actually rent needs to add it on a Tuesday.
/// </para>
/// <para>
/// Admin-only, and deliberately not staff-wide: an accommodation type is referenced by every
/// listing that claimed it and by every client's search form, so it is closer to a schema change
/// than to a moderation decision.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.ManageCatalogue)]
[Auditable]
public sealed record UpsertAccommodationTypeCommand : ICommand<AccommodationTypeResponse>
{
    public required UpsertAccommodationTypeRequest Request { get; init; }
}
