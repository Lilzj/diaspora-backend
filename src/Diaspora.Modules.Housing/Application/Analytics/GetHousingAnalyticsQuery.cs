using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Analytics;

/// <summary>
/// The measured record for one property.
/// <para>
/// Readable by its landlord and by staff. The pairing that matters is viewing requests against
/// views: a property with three thousand views and no viewing requests is priced wrong,
/// photographed badly, or not real.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.ViewAnalytics)]
public sealed record GetHousingAnalyticsQuery : IQuery<HousingAnalyticsResponse>
{
    public required Guid ListingId { get; init; }
}
