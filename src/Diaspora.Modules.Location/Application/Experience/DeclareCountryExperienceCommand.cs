using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Requests;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Experience;

/// <summary>
/// Declares — or amends — the caller's residence in a country.
/// <para>
/// Upsert rather than create, on purpose. One row per member per country is the aggregate's
/// identity, and the client's own flow re-posts the whole record on every edit. A create-only
/// endpoint would make the second save a conflict the client would have to handle by
/// discovering it first.
/// </para>
/// </summary>
[Authorize(Permission = LocationPermissions.ManageExperience)]
public sealed record DeclareCountryExperienceCommand : ICommand<CountryExperienceResponse>
{
    public required DeclareCountryExperienceRequest Experience { get; init; }
}
