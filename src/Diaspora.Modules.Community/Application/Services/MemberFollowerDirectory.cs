using Diaspora.Contracts.Community.V1.Follows;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Follows;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Community's answer to <see cref="IMemberFollowerDirectory"/>: the unmuted followers of a member,
/// a page at a time.
/// </summary>
public sealed class MemberFollowerDirectory : IMemberFollowerDirectory
{
    /// <summary>A page larger than this is the caller paging badly, not a bigger audience.</summary>
    private const int MaximumPage = 500;

    private readonly IFollowRepository _follows;

    public MemberFollowerDirectory(IFollowRepository follows) => _follows = follows;

    public async Task<IReadOnlyList<Guid>> ListFollowerPageAsync(
        Guid memberUserId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (memberUserId == Guid.Empty || take <= 0)
        {
            return [];
        }

        return await _follows.ListUnmutedFollowerPageAsync(
            FollowTargetType.Member,
            memberUserId,
            Math.Max(0, skip),
            Math.Min(take, MaximumPage),
            cancellationToken);
    }
}
