using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Reads one guide in full, by the handle a deep link carries.
/// <para>
/// The slug is the address knowledge is shared at — in a message, a search result, a printed
/// leaflet — so it resolves a guide as completely as the identifier does. Same visibility rules,
/// same "not found" for anything the caller may not see.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record GetGuideBySlugQuery : IQuery<GuideResponse>
{
    public required string Slug { get; init; }
}
