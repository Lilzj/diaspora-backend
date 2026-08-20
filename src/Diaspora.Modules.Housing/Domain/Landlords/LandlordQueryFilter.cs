using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Policies;

namespace Diaspora.Modules.Housing.Domain.Landlords;

/// <summary>How a landlord directory is narrowed.</summary>
public sealed record LandlordQueryFilter
{
    public string? CountryCode { get; init; }

    public IReadOnlyCollection<LandlordKind>? Kinds { get; init; }

    public IReadOnlyCollection<LandlordStatus>? Statuses { get; init; }

    /// <summary>Verified landlords only, read from the cached Trust mark.</summary>
    public bool VerifiedOnly { get; init; }

    /// <summary>A B7 provider, for "which landlord profile is this agency".</summary>
    public Guid? ProviderId { get; init; }

    public string? Text { get; init; }

    public int Limit { get; init; } = HousingLimits.DefaultPageSize;

    public int EffectiveLimit => Math.Clamp(Limit, 1, HousingLimits.MaxPageSize);
}
