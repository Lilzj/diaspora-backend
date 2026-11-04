namespace Diaspora.Contracts.CommunityProgramme.V1.Directory;

/// <summary>
/// The communities a member belongs to — 4 Nov 2026, "Recommended" on the events screen.
/// <para>
/// <b>Why a seam.</b> The events screen offers the events posted into the member's own communities.
/// Events belong to Community Life, which composes at 1100; communities belong to Community
/// Programme, which composes at 1600 and which Community Life may therefore not reference — the
/// reason <see cref="ICommunityEventPostingAuthority"/> gives. With no implementation registered, a
/// member belongs to no community and the list is empty.
/// </para>
/// <para>
/// <b>A current member only</b>: somebody following, interested or waiting on approval is not in
/// the community yet, and its events are not "theirs".
/// </para>
/// </summary>
public interface IMemberCommunityDirectory
{
    /// <summary>The identifiers of the communities <paramref name="memberId"/> is a current member of.</summary>
    Task<IReadOnlyList<Guid>> CommunitiesOfAsync(Guid memberId, CancellationToken cancellationToken = default);
}
