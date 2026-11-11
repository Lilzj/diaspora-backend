using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Soft-deletes a property.
/// <para>
/// Soft only. A deleted listing keeps its slug, its analytics and its reservation history, because
/// somebody may still be living there under an arrangement it records.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.ListingDeleteOwn)]
[Auditable]
public sealed record DeleteHousingListingCommand : ICommand
{
    public required Guid ListingId { get; init; }
}
