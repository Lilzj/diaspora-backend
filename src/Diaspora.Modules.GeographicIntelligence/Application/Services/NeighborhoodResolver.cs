using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.GeographicIntelligence.Application.Services;

/// <summary>
/// Turns the client's neighbourhood route parameter into B4's area.
/// <para>
/// <b>Identifier first, then slug, exactly as B4's own hub does.</b> A GUID is unambiguous and a
/// slug is not; a slug that happens to parse as a GUID does not exist. Duplicating the order here
/// rather than inventing a new one is what keeps <c>/neighborhoods/peckham/scores</c> resolving to
/// the same place as <c>/location/neighborhoods/peckham</c> — two routes that disagreed about
/// which Peckham they meant would be worse than either being wrong.
/// </para>
/// <para>
/// The register asks for "a stable neighbourhood identifier scheme; ids are currently derived by
/// slugifying a name, which breaks on rename and collides across cities." B4's <c>AreaId</c> is
/// that scheme and it has existed all along; this resolver accepts it, and every response carries
/// it back so a client can migrate its links at its own pace.
/// </para>
/// </summary>
public sealed class NeighborhoodResolver
{
    private readonly IAdministrativeAreaRepository _areas;

    public NeighborhoodResolver(IAdministrativeAreaRepository areas) => _areas = areas;

    public async Task<AdministrativeArea?> ResolveAsync(
        string? idOrSlug, string? countryCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idOrSlug))
        {
            return null;
        }

        if (Guid.TryParse(idOrSlug, out var id))
        {
            var byId = await _areas.GetByIdAsync(AreaId.From(id), cancellationToken);

            if (byId?.Level == AdministrativeLevel.Neighborhood)
            {
                return byId;
            }
        }

        var slug = LocationSlug.TryCreate(idOrSlug);

        if (slug.IsFailure)
        {
            return null;
        }

        var country = string.IsNullOrWhiteSpace(countryCode)
            ? null
            : CountryCode.TryCreate(countryCode) is { IsSuccess: true } parsed
                ? parsed.Value
                : null;

        return await _areas.GetBySlugAsync(
            slug.Value, AdministrativeLevel.Neighborhood, country, cancellationToken);
    }
}
