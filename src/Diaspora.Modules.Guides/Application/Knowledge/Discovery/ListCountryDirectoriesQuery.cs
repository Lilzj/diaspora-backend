using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// Every country the platform holds knowledge for — the "browse by country" grid.
/// <para>
/// Deliberately unpaged. There are fewer than two hundred countries, the answer fits in one
/// response, and a cursor over a list that short is machinery nobody needs.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListCountryDirectoriesQuery
    : IQuery<IReadOnlyList<CountryDirectorySummaryResponse>>
{
    /// <summary>A BCP-47 tag. Country names come back resolved for it.</summary>
    public string? Locale { get; init; }

    /// <summary>
    /// Unpublished directories too. Honoured only for a curator — the launch schedule is not
    /// something a reader should be able to enumerate.
    /// </summary>
    public bool IncludeUnpublished { get; init; }
}
