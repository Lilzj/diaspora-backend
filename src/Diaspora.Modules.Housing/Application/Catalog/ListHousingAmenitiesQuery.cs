using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Catalog;

/// <summary>The amenity catalogue, grouped as a listing form renders it.</summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListHousingAmenitiesQuery
    : IQuery<IReadOnlyList<HousingAmenityCatalogResponse>>
{
    public bool IncludeInactive { get; init; }
}
