using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Domain.Members;

/// <summary>
/// Community's own answer to "is this a member, and what do we call them".
/// <para>
/// The read side of <see cref="CommunityMember"/>. Deliberately narrow: existence and a name. A
/// directory that could list or search members would be a member-enumeration surface living in the
/// module that also accepts reports, which is precisely the combination BG-05a must not create.
/// </para>
/// </summary>
public interface ICommunityMemberDirectory
{
    /// <summary>The member, or null when Community has never seen them.</summary>
    Task<CommunityMember?> FindAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Many at once, for B6's moderation queue.
    /// <para>
    /// Unlike a direct message, a member is not private information to a moderator holding a report
    /// about them — the whole point of the queue is to show whom the case concerns — so this one
    /// genuinely resolves, and a page of member reports renders with names rather than identifiers.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, CommunityMember>> FindManyAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    void Add(CommunityMember member);

    void Update(CommunityMember member);
}
