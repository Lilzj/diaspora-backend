using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityProgramme.Domain.Repositories;

/// <summary>A community's published rules, in order. Visibility-blind; the handler decides who may read.</summary>
public interface ICommunityRuleRepository
{
    Task<IReadOnlyList<CommunityRule>> ListAsync(
        CommunityId communityId, CancellationToken cancellationToken = default);

    Task<CommunityRule?> GetAsync(
        CommunityId communityId, CommunityRuleId id, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CommunityId communityId, CancellationToken cancellationToken = default);

    void Add(CommunityRule rule);

    void Remove(CommunityRule rule);
}

/// <summary>A community's notices. Visibility-blind; the handler decides who may read.</summary>
public interface ICommunityNoticeRepository
{
    /// <summary>Pinned first, then newest first. Lapsed notices only when asked for.</summary>
    Task<IReadOnlyList<CommunityNotice>> ListAsync(
        CommunityId communityId,
        bool includeExpired,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default);

    Task<CommunityNotice?> GetAsync(
        CommunityId communityId, CommunityNoticeId id, CancellationToken cancellationToken = default);

    /// <summary>Notices published in <c>[from, until)</c>. For statistics.</summary>
    Task<int> CountPublishedAsync(
        CommunityId communityId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);

    void Add(CommunityNotice notice);

    void Remove(CommunityNotice notice);
}

/// <summary>One member's community preferences. At most one row per member.</summary>
public interface ICommunityMemberPreferencesRepository
{
    Task<CommunityMemberPreferences?> FindAsync(
        UserId memberId, CancellationToken cancellationToken = default);

    void Add(CommunityMemberPreferences preferences);
}

/// <summary>
/// Counted facts about a community's membership, for its statistics — 21 Oct 2026.
/// <para>
/// Read straight off <c>community_memberships</c>: the record of who belongs is the only thing on the
/// platform that knows, so every figure here is a count of rows rather than an estimate.
/// </para>
/// </summary>
public interface ICommunityMembershipStatistics
{
    Task<int> CountCurrentMembersAsync(
        CommunityId communityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// When each membership in the window began. A member who joined and later left is still a join
    /// in the period they joined; a member who rejoined counts at their latest join.
    /// </summary>
    Task<IReadOnlyList<DateTimeOffset>> ListJoinTimesAsync(
        CommunityId communityId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);

    /// <summary>Memberships that ended — left or removed — in <c>[from, until)</c>.</summary>
    Task<int> CountDeparturesAsync(
        CommunityId communityId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);

    /// <summary>The user identifiers of every current member, for counting their activity elsewhere.</summary>
    Task<IReadOnlyList<Guid>> ListCurrentMemberIdsAsync(
        CommunityId communityId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Current members holding a role, oldest membership first. <paramref name="includeCommunityOnly"/>
    /// adds those visible to the community only; Private memberships are never returned (a role
    /// cannot be held on one).
    /// </summary>
    Task<IReadOnlyList<CommunityMembership>> ListRoleHoldersAsync(
        CommunityId communityId,
        string? role,
        bool includeCommunityOnly,
        int limit,
        CancellationToken cancellationToken = default);
}
