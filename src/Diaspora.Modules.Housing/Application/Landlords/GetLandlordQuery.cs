using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Landlords;

/// <summary>
/// One landlord profile, by identifier or slug.
/// <para>
/// Unauthenticated, because a member deciding whether to trust a letting agency should be able to
/// look it up before they join anything. What they see is redacted accordingly.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetLandlordQuery : IQuery<LandlordProfileResponse>
{
    public required string IdOrSlug { get; init; }
}
