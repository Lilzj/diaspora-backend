using Diaspora.Contracts.Community.V1.Activity;
using Diaspora.Modules.Community.Domain.Posts;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Community's answer to <see cref="IMemberPostActivityReader"/>: how much a set of members published.
/// Counts only — see the contract.
/// </summary>
public sealed class MemberPostActivityReader : IMemberPostActivityReader
{
    /// <summary>More than this is a caller counting the platform, not a community.</summary>
    private const int MaximumMembers = 5000;

    private readonly ICommunityPostRepository _posts;

    public MemberPostActivityReader(ICommunityPostRepository posts) => _posts = posts;

    public async Task<MemberPostActivity> CountAsync(
        IReadOnlyCollection<Guid> memberUserIds,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberUserIds);

        if (memberUserIds.Count == 0 || until <= from)
        {
            return new MemberPostActivity(0, 0);
        }

        var (posts, authors) = await _posts.CountPublishedByAuthorsAsync(
            [.. memberUserIds.Where(id => id != Guid.Empty).Take(MaximumMembers)],
            from,
            until,
            cancellationToken);

        return new MemberPostActivity(posts, authors);
    }
}
