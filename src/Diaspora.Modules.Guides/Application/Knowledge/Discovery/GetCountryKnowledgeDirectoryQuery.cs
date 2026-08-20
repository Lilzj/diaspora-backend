using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// One country's knowledge directory — the whole country page in one response.
/// <para>
/// <b>The canonical entry point into country-specific knowledge, and the reason this phase
/// exists.</b> Everything the page shows lives somewhere else: the arrangement on the directory, the
/// labels in the catalogue, the guides in B8, the counts in three projections. A client assembling
/// it would need twenty round trips and would get the localization fallback wrong.
/// </para>
/// <para>
/// Unauthenticated. A knowledge base that demands an account before it will tell somebody what to
/// read about the country they are moving to has no way of persuading them to make one.
/// </para>
/// </summary>
public sealed record GetCountryKnowledgeDirectoryQuery : IQuery<CountryKnowledgeDirectoryResponse>
{
    /// <summary>ISO 3166-1 alpha-2. Case-insensitive on the way in.</summary>
    public required string CountryCode { get; init; }

    /// <summary>A BCP-47 tag. Labels and copy come back resolved for it.</summary>
    public string? Locale { get; init; }

    /// <summary>Narrows the guide counts to one language. Absent counts every language.</summary>
    public string? Language { get; init; }
}
