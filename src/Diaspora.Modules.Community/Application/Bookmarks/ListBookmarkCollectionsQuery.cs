using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Bookmarks;

/// <summary>The saved screen: type-derived tiles and member-named folders together.</summary>
[Authorize(Policy = CommunityPolicies.Bookmark)]
public sealed record ListBookmarkCollectionsQuery : IQuery<IReadOnlyList<BookmarkCollectionResponse>>;
