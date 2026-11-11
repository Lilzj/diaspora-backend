using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Tags;

/// <summary>
/// The tag cloud — which tags are actually being used, and how much.
/// <para>
/// Public, and built from real usage rather than from a curated vocabulary: tags are the part of
/// the taxonomy authors control, and a cloud assembled from a fixed list would tell a member
/// what somebody once thought they would write about rather than what they wrote.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListGuideTagsQuery : IQuery<IReadOnlyList<GuideTagResponse>>
{
    /// <summary>Narrows the sample to one country. Null samples everywhere.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Narrows the sample to one category. Null samples the whole catalogue.</summary>
    public string? CategoryKey { get; init; }

    /// <summary>Tags to return, best first. Null takes the default cloud size.</summary>
    public int? Limit { get; init; }
}
