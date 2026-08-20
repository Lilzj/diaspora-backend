namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One navigation row, opened: the category page for one country.
/// <para>
/// The overview, then whichever shelves the deployment's registered section providers produced. A
/// client renders the sections in the order given and needs to know nothing about which of them
/// exist — which is what makes the section seam usable: a deployment that adds a shelf gets it on
/// every category page in every client, with no client change.
/// </para>
/// </summary>
public sealed record KnowledgeCategoryViewResponse
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Already localized for the caller.</summary>
    public required string CountryName { get; init; }

    /// <summary>The row itself, localized, with its sources and recommended provider categories.</summary>
    public required KnowledgeCategoryResponse Category { get; init; }

    /// <summary>The country's own wording for this row, when a curator set one.</summary>
    public string? Label { get; init; }

    /// <summary>Published guides this row resolves to for this country.</summary>
    public required int GuideCount { get; init; }

    /// <summary>The shelves, in render order. Empty shelves are absent.</summary>
    public IReadOnlyList<KnowledgeSectionResponse> Sections { get; init; } = [];

    /// <summary>
    /// Provider service categories to offer beside this subject, as keys.
    /// <para>
    /// Keys rather than providers, because which immigration consultants exist in this country is
    /// B7's question and is answered by B7's own endpoints, with its own visibility and verification
    /// rules. The directory says which kind of professional this subject calls for.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> ProviderCategoryKeys { get; init; } = [];
}
