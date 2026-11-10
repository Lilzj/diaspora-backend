using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Suggestions;

/// <summary>
/// The improvement suggestions on one guide.
/// <para>
/// Public for a published guide, deliberately: seeing that somebody has already reported the fee
/// as out of date is useful to the next reader, and it stops the author receiving the same
/// correction ten times. What is <em>not</em> public is deciding them.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListGuideSuggestionsQuery : IQuery<CursorPagedResult<GuideSuggestionResponse>>
{
    public required Guid GuideId { get; init; }

    /// <summary>A <c>GuideSuggestionStatus</c> name to narrow to. Null returns every status.</summary>
    public string? Status { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
