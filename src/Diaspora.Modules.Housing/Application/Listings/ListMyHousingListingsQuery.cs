using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// The caller own properties, drafts included.
/// <para>
/// Deliberately readable by a suspended landlord: somebody barred from letting must still be able
/// to see their properties and read why, and a screen that went blank on suspension would leave
/// them unable to correct whatever caused it.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.ListingEditOwn)]
public sealed record ListMyHousingListingsQuery : IQuery<HousingSearchResultResponse>
{
    /// <summary>Narrow to one lifecycle state. Null returns everything of theirs.</summary>
    public string? Status { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
