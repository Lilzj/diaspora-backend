namespace Diaspora.Contracts.CommunityProgramme.V1.Directory;

/// <summary>
/// Whether a member may post an event into a community — 21 Oct 2026, "Host picks a community".
/// <para>
/// <b>Why a seam.</b> Events belong to Community Life, which composes at 1100; communities belong to
/// Community Programme, which composes at 1600 and which Community Life may therefore not reference.
/// The same shape as <c>IPublisherAuthority</c>: the module that owns the community answers, through
/// <c>Diaspora.Contracts</c>, from the same capability set its own capabilities route gives the app —
/// so "Post in community" is offered and allowed by one rule. With no implementation registered,
/// nobody may post an event into any community.
/// </para>
/// <para>
/// <b>The rule is <c>canPostToCommunity</c></b>: a current member of an Active community. A pending,
/// suspended or archived community takes no events, and somebody who is only following, interested
/// or waiting on approval may not post into one.
/// </para>
/// </summary>
public interface ICommunityEventPostingAuthority
{
    /// <summary>
    /// The community when <paramref name="memberId"/> may post an event into
    /// <paramref name="communityId"/>, or null when they may not or no such community exists. A null
    /// never says which of the two it was.
    /// </summary>
    Task<CommunityEventPostingGrant?> AuthorizeAsync(
        Guid memberId, Guid communityId, CancellationToken cancellationToken = default);
}

/// <summary>The community an event may be posted into, and its name for the event page.</summary>
public sealed record CommunityEventPostingGrant(Guid CommunityId, string Name, string Slug);
