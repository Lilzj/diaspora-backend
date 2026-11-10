using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Discovery;

/// <summary>
/// Search housing.
/// <para>
/// Unauthenticated. Somebody deciding whether they can afford to move to a country needs to see
/// what is available and what it costs before they join anything, and a search behind a sign-in
/// would be useful only to people who had already decided.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record SearchHousingQuery : IQuery<HousingSearchResultResponse>
{
    public required SearchHousingRequest Request { get; init; }
}
