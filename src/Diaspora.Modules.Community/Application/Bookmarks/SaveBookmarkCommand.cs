using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Bookmarks;

/// <summary>Saves anything the platform can address.</summary>
[Auditable]
[Authorize(Policy = CommunityPolicies.Bookmark)]
public sealed record SaveBookmarkCommand : ICommand<BookmarkResponse>
{
    public required CreateBookmarkRequest Request { get; init; }
}
