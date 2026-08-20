using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Domain.Viewings;

/// <summary>
/// How a viewing list is narrowed.
/// <para>
/// <b>Every viewing query is scoped to a party.</b> A viewing is private to the member and the
/// landlord, so either <see cref="SeekerUserId"/> or <see cref="LandlordId"/> is always set by the
/// handler - there is no unscoped listing of viewings, and staff get no bypass here. Reading a
/// named person's appointments is a different act from moderating a public listing.
/// </para>
/// </summary>
public sealed record ViewingQueryFilter
{
    /// <summary>The member's own viewings.</summary>
    public UserId? SeekerUserId { get; init; }

    /// <summary>The landlord's diary.</summary>
    public LandlordProfileId? LandlordId { get; init; }

    public HousingListingId? ListingId { get; init; }

    public IReadOnlyCollection<HousingViewingStatus>? Statuses { get; init; }

    /// <summary>Only viewings that are still ahead of the parties.</summary>
    public bool OpenOnly { get; init; }

    /// <summary>Viewings whose slot falls on or after this moment. What a diary view asks for.</summary>
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? Until { get; init; }

    public int Limit { get; init; } = HousingLimits.DefaultPageSize;

    public int EffectiveLimit => Math.Clamp(Limit, 1, HousingLimits.MaxPageSize);
}
