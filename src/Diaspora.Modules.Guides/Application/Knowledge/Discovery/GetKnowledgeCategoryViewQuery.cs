using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// One navigation row, opened: the category page for one country, with every registered shelf.
/// <para>
/// The shelves come from whichever section providers this deployment registered, so a client renders
/// what it is given and needs to know nothing about which exist. That is what makes the section seam
/// usable: a deployment that adds a shelf gets it on every category page in every client, with no
/// client change.
/// </para>
/// </summary>
public sealed record GetKnowledgeCategoryViewQuery : IQuery<KnowledgeCategoryViewResponse>
{
    public required string CountryCode { get; init; }

    /// <summary>The navigation row key. Expanded server-side into its filing categories.</summary>
    public required string KnowledgeCategoryKey { get; init; }

    public string? Locale { get; init; }

    public string? Language { get; init; }

    /// <summary>Guides per shelf. Clamped; a shelf is a shelf, not a browse.</summary>
    public int? Limit { get; init; }
}
