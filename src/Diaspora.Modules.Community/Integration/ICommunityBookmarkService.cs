using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Integration;

/// <summary>Bookmarks, for a module that owns content.</summary>
public interface ICommunityBookmarkService
{
    Task<Result> SaveAsync(
        ContentReference content,
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<Result> UnsaveAsync(
        ContentReference content,
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> FilterSavedAsync(
        UserId userId,
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default);
}
