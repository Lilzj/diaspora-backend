using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityProgramme.Domain.Repositories;

/// <summary>
/// A community's initiatives — listing them, loading one, creating one, and the summary figures a
/// card shows. Visibility-blind, as the rules and notices are; the handler decides who may read.
/// <para>
/// <b>Every participant count and listing is joined to the community's roll.</b> A participant who
/// left or was removed from the community keeps their row here, and is neither counted nor listed:
/// the roll is the only truth about who belongs, and reading participation without it would count
/// people who are no longer there. That holds for the counts here and for every member of
/// <see cref="ICommunityInitiativeParticipants"/>.
/// </para>
/// <para>
/// One of three roles that used to be one interface. Its participants and its updates are
/// separate entities with separate lifecycles, and nine of this module's handler classes used to
/// take all fourteen members to touch one of them.
/// </para>
/// </summary>
public interface ICommunityInitiativeRepository
{
    /// <summary>Newest first, optionally of one status. Untracked.</summary>
    Task<IReadOnlyList<CommunityInitiative>> ListAsync(
        CommunityId communityId,
        CommunityInitiativeStatus? status,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Tracked, for the write handlers.</summary>
    Task<CommunityInitiative?> GetAsync(
        CommunityId communityId, CommunityInitiativeId id, CancellationToken cancellationToken = default);

    void Add(CommunityInitiative initiative);

    /// <summary>Participants who are still current members, per initiative. Absent keys are zero.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountParticipantsAsync(
        CommunityId communityId,
        IReadOnlyCollection<CommunityInitiativeId> initiativeIds,
        CancellationToken cancellationToken = default);

    /// <summary>Updates posted, per initiative. Absent keys are zero.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountUpdatesAsync(
        IReadOnlyCollection<CommunityInitiativeId> initiativeIds,
        CancellationToken cancellationToken = default);

    /// <summary>Which of these initiatives the member has joined.</summary>
    Task<IReadOnlySet<Guid>> ListJoinedAsync(
        UserId memberId,
        IReadOnlyCollection<CommunityInitiativeId> initiativeIds,
        CancellationToken cancellationToken = default);

    /// <summary>These people's rows in this community, whatever their state. Untracked.</summary>
    Task<IReadOnlyList<CommunityMembership>> ListMembershipsAsync(
        CommunityId communityId,
        IReadOnlyCollection<UserId> memberIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Joining, leaving, being forgotten, and who is visible on the participant list.
/// <para>
/// Every listing and count here is joined to the community's roll, for the reason
/// <see cref="ICommunityInitiativeRepository"/> records: a participant who left the community
/// keeps their row and is neither counted nor listed.
/// </para>
/// <para>
/// <see cref="ForgetParticipant"/> is the erasure path rather than a leave, which is why this role
/// changes when the privacy rules do.
/// </para>
/// </summary>
public interface ICommunityInitiativeParticipants
{
    Task<CommunityInitiativeParticipant?> FindParticipantAsync(
        CommunityInitiativeId initiativeId, UserId memberId, CancellationToken cancellationToken = default);

    void AddParticipant(CommunityInitiativeParticipant participant);

    void RemoveParticipant(CommunityInitiativeParticipant participant);

    /// <summary>
    /// Stops tracking a participant whose write the database refused — the losing side of two
    /// simultaneous joins or leaves — so nothing later in the scope tries to write it again.
    /// </summary>
    void ForgetParticipant(CommunityInitiativeParticipant participant);

    /// <summary>
    /// Participants the viewer may see, earliest first: those whose membership is Public, plus
    /// CommunityOnly ones when <paramref name="includeCommunityOnly"/>, plus the viewer themself.
    /// Never a Private membership belonging to somebody else.
    /// </summary>
    Task<IReadOnlyList<CommunityInitiativeParticipant>> ListVisibleParticipantsAsync(
        CommunityId communityId,
        CommunityInitiativeId initiativeId,
        bool includeCommunityOnly,
        UserId? viewer,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// An initiative's updates: posting one, and reading the feed.
/// <para>
/// Two members, two consumers, one of each. The narrowest role in the family and the clearest
/// illustration of the point: posting an update has nothing to do with participation.
/// </para>
/// </summary>
public interface ICommunityInitiativeUpdates
{
    /// <summary>Newest first. Untracked.</summary>
    Task<IReadOnlyList<CommunityInitiativeUpdate>> ListUpdatesAsync(
        CommunityInitiativeId initiativeId, int limit, CancellationToken cancellationToken = default);

    void AddUpdate(CommunityInitiativeUpdate update);
}
