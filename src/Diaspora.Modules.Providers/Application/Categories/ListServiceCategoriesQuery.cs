using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Categories;

/// <summary>
/// The service category catalogue.
/// <para>
/// The first call every client makes: the discovery tab renders its category chip row before
/// anything else. Returned flat and ordered by path — already depth-first — so a client can draw
/// a row or build a tree from the same payload.
/// </para>
/// </summary>
public sealed record ListServiceCategoriesQuery : IQuery<IReadOnlyList<ServiceCategoryResponse>>
{
    /// <summary>Only this node's children. Null returns the whole tree.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Staff-only. Retired categories still resolve; they are not offered.</summary>
    public bool IncludeRetired { get; init; }

    public string? LanguageCode { get; init; }
}
