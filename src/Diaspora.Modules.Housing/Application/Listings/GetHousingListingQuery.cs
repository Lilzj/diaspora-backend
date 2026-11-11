using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// One property, by identifier or slug.
/// <para>
/// Unauthenticated. Somebody deciding whether to move to a country should be able to see what
/// living there costs before they join anything; what they see is redacted accordingly, and the
/// address never appears without a confirmed viewing.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetHousingListingQuery : IQuery<HousingListingResponse>
{
    public required string IdOrSlug { get; init; }

    /// <summary>
    /// Whether to count this as a view. False for a landlord previewing their own listing and for
    /// any internal read, so a dashboard refresh does not inflate the figure it is displaying.
    /// </summary>
    public bool RecordView { get; init; } = true;
}
