using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Versioning;

/// <summary>
/// What changed between two published versions.
/// <para>
/// The question a returning reader actually has — "is this different from what I did last
/// year?" — answered at section granularity, which is the unit a person reads in. Sections are
/// matched by their stable identifiers, so a retitle and a replacement stay distinguishable.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record CompareGuideVersionsQuery : IQuery<GuideVersionDiffResponse>
{
    public required Guid GuideId { get; init; }

    /// <summary>The older version number.</summary>
    public required int FromVersion { get; init; }

    /// <summary>The newer version number.</summary>
    public required int ToVersion { get; init; }
}
