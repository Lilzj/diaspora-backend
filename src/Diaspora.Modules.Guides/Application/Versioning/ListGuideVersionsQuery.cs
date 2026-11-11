using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Versioning;

/// <summary>
/// A guide's publication history, newest first.
/// <para>
/// Public, because the history <em>is</em> the accountability: a reader who acted on advice that
/// has since changed should be able to see that it changed, and when. Summaries only — a history
/// of fifty versions must not carry fifty documents.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListGuideVersionsQuery : IQuery<IReadOnlyList<GuideVersionSummaryResponse>>
{
    public required Guid GuideId { get; init; }

    /// <summary>Rows to return, bounded by the listing ceiling. Null takes the ceiling.</summary>
    public int? Limit { get; init; }
}
