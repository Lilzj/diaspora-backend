using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Bookmarks;

/// <summary>Creates a member-named folder.</summary>
[Auditable]
[RequirePolicy(CommunityPolicies.Bookmark)]
public sealed record CreateBookmarkFolderCommand : ICommand<BookmarkFolderResponse>
{
    public required CreateBookmarkFolderRequest Request { get; init; }
}
