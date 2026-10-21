using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Application.Presence;

/// <summary>
/// The presence rules that are not a request's: the interval, the accuracy floor, and an area's
/// chain of enclosing areas.
/// </summary>
public sealed class PresenceAreas
{
    /// <summary>
    /// The shortest gap between two evaluated reports. A constant rather than a setting: it is a
    /// floor against a misbehaving client, not a tuning knob, and the app is told it on every read.
    /// </summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The coarsest fix evaluated. The tightest rung the area locator files at is a neighbourhood
    /// five kilometres from its centre; a fix vaguer than a kilometre could put the member on either
    /// side of a neighbourhood boundary and announce an arrival that did not happen.
    /// </summary>
    public const double MaximumAccuracyMetres = 1000d;

    private readonly IAdministrativeAreaRepository _areas;

    public PresenceAreas(IAdministrativeAreaRepository areas) => _areas = areas;

    /// <summary>
    /// The area and every area that holds it, most specific first, or empty when the area is gone.
    /// </summary>
    public async Task<(AdministrativeArea? Area, IReadOnlyList<Guid> Chain)> ChainAsync(
        Guid? areaId, CancellationToken cancellationToken)
    {
        if (areaId is not { } id || id == Guid.Empty)
        {
            return (null, []);
        }

        var area = await _areas.GetByIdAsync(AreaId.From(id), cancellationToken);

        if (area is null)
        {
            return (null, []);
        }

        var ancestors = await _areas.GetAncestorsAsync(area, cancellationToken);

        // Ancestors come root first; the chain reads from where the member is outwards.
        return (area, [area.Id.Value, .. ancestors.Reverse().Select(ancestor => ancestor.Id.Value)]);
    }

    public static PresenceAreaResponse? Describe(AdministrativeArea? area) =>
        area is null
            ? null
            : new PresenceAreaResponse
            {
                AreaId = area.Id.Value,
                Name = area.Name.Value,
                Level = area.Level.ToString(),
                CountryCode = area.CountryCode.Value
            };
}
