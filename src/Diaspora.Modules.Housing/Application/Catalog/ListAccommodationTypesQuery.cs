using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Catalog;

/// <summary>
/// The accommodation-type catalogue.
/// <para>
/// Unauthenticated: a client needs the picker before it can render a search form, and the catalogue
/// is reference data rather than anybody content.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListAccommodationTypesQuery
    : IQuery<IReadOnlyList<AccommodationTypeResponse>>
{
    /// <summary>Retired entries too. Staff only in practice, for a management screen.</summary>
    public bool IncludeInactive { get; init; }
}
