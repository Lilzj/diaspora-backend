using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Domain.Repositories;

/// <summary>
/// What the directory may be asked.
/// <para>
/// <b>Every field here is a filter the route actually binds.</b> The register's own history is the
/// reason to say so: an unbound parameter is discarded by the minimal-API model binder with no
/// error, so a client sent a filter, got a 200, and the chip stayed lit over an unfiltered list.
/// Anything this record cannot express is a filter the directory does not offer.
/// </para>
/// <para>
/// <b>Sorting by member count is not here.</b> §31 forbids implying a popularity the backend does
/// not authoritatively report, and the count is maintained by the aggregate rather than measured.
/// When a deployment can vouch for it, this record grows a sort — until then, offering one that
/// silently fell back to name would be the same defect in a new place.
/// </para>
/// </summary>
public sealed record CommunityDirectoryFilter
{
    /// <summary>Matches the name and the tagline. Null means everything.</summary>
    public string? Query { get; init; }

    public IReadOnlyList<CommunityType>? Types { get; init; }

    /// <summary>Where communities are *from*.</summary>
    public IReadOnlyList<string>? OriginCountryCodes { get; init; }

    /// <summary>Where communities have a *presence*, which is a different question.</summary>
    public string? PresenceCountryCode { get; init; }

    /// <summary>
    /// The caller, so their own standing can be read in the same page.
    /// <para>
    /// Null for an unauthenticated reader, who sees public communities only and a membership state
    /// of <c>None</c> — never an error. Reading a directory does not require an account.
    /// </para>
    /// </summary>
    public UserId? Viewer { get; init; }

    /// <summary>
    /// Communities to leave out, resolved by the caller.
    /// <para>
    /// This is how <c>includeJoined=false</c> is served, and it is applied in the query rather than
    /// to the materialised page for a reason worth stating: dropping rows after paging returns short
    /// pages, and a short page reads to an infinite-scroll list as the end of the directory. A
    /// member who belongs to eight of the first twenty communities would see a twelve-row list and
    /// no more.
    /// </para>
    /// </summary>
    public IReadOnlyList<CommunityId>? ExcludeCommunityIds { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = CommunityProgrammeLimits.DefaultPageSize;
}

/// <summary>
/// Reads and writes communities.
/// <para>
/// <b>Slug uniqueness lives here, not on the value object.</b> <see cref="CommunitySlug"/> can
/// guarantee a handle's shape and nothing about whether anybody else holds it — it cannot see the
/// other rows. The unique index is the real enforcement; <see cref="IsSlugTakenAsync"/> exists so a
/// caller gets a useful refusal instead of a database exception.
/// </para>
/// </summary>
public interface ICommunityRepository
{
    Task<CommunityAggregate?> GetAsync(CommunityId id, CancellationToken cancellationToken = default);

    /// <summary>Reads by handle. The lookup a deep link performs.</summary>
    Task<CommunityAggregate?> FindBySlugAsync(
        CommunitySlug slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads by whichever of the two the caller supplied.
    /// <para>
    /// The route is <c>/communities/{idOrSlug}</c> because a link carries a slug and a client that
    /// already loaded a community carries an id, and making them two routes would mean the client
    /// deciding which to call. A value that parses as a Guid is read as one; anything else is read
    /// as a handle.
    /// </para>
    /// </summary>
    Task<CommunityAggregate?> FindByIdOrSlugAsync(
        string idOrSlug, CancellationToken cancellationToken = default);

    /// <summary>Whether a handle is already held. See the interface summary.</summary>
    Task<bool> IsSlugTakenAsync(CommunitySlug slug, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<CommunityAggregate>> SearchAsync(
        CommunityDirectoryFilter filter, CancellationToken cancellationToken = default);

    void Add(CommunityAggregate community);
}

/// <summary>
/// Reads and writes one person's standing in one community.
/// <para>
/// <b>"At most one primary" is a rule no aggregate can hold.</b> A membership cannot see the
/// member's other memberships, so <see cref="FindPrimaryAsync"/> exists to let a handler clear the
/// old one before setting a new one. The filtered unique index is what makes that non-negotiable
/// rather than a convention.
/// </para>
/// </summary>
public interface ICommunityMembershipRepository
{
    Task<CommunityMembership?> GetAsync(
        CommunityMembershipId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The member's row for one community, whatever state it is in.
    /// <para>
    /// Returns a row for somebody who left or was revoked, because those are the two cases a caller
    /// most needs to see — the row's existence is not standing, and every reader has to say which
    /// states it counts.
    /// </para>
    /// </summary>
    Task<CommunityMembership?> FindAsync(
        CommunityId communityId, UserId memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every community this member has a row in, keyed by community.
    /// <para>
    /// One query per directory page rather than one per row. A page of fifty communities that asked
    /// the database fifty times for the caller's own state would be the N+1 that makes a directory
    /// unusable at the scale §77 requires it to survive.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<CommunityId, CommunityMembership>> FindForMemberAsync(
        UserId memberId,
        IReadOnlyCollection<CommunityId>? communityIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>The member's primary, if they have set one.</summary>
    Task<CommunityMembership?> FindPrimaryAsync(
        UserId memberId, CancellationToken cancellationToken = default);

    void Add(CommunityMembership membership);
}
