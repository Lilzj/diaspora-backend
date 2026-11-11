using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// The author's unpublished work — what they were in the middle of writing.
/// <para>
/// A separate screen from the workspace because it answers a different question: not "what have
/// I written" but "what have I not finished". A draft has an audience of one, which is why this
/// read is permissioned where the rest of discovery is not.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.ViewDrafts)]
public sealed record GetMyDraftsQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
