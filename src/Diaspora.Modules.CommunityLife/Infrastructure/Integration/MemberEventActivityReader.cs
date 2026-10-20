using Diaspora.Contracts.CommunityLife.V1.Activity;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Integration;

/// <summary>
/// Community Life's answer to <see cref="IMemberEventActivityReader"/>: how many events a set of
/// members published. Counts only — see the contract.
/// </summary>
public sealed class MemberEventActivityReader : IMemberEventActivityReader
{
    /// <summary>More than this is a caller counting the platform, not a community.</summary>
    private const int MaximumMembers = 5000;

    private readonly ICommunityEventRepository _events;

    public MemberEventActivityReader(ICommunityEventRepository events) => _events = events;

    public async Task<MemberEventActivity> CountAsync(
        IReadOnlyCollection<Guid> memberUserIds,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberUserIds);

        if (memberUserIds.Count == 0 || until <= from)
        {
            return new MemberEventActivity(0, 0);
        }

        var (events, organizers) = await _events.CountFirstPublishedByOrganizersAsync(
            [.. memberUserIds.Where(id => id != Guid.Empty).Take(MaximumMembers)],
            from,
            until,
            cancellationToken);

        return new MemberEventActivity(events, organizers);
    }
}
