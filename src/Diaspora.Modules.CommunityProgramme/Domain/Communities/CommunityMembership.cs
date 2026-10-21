using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Communities;

/// <summary>
/// One person's standing in one community, and the whole of it.
/// <para>
/// <b>The route that exists today is not this.</b> <c>PUT /communities/{organizationId}/membership</c>
/// opens an organization's membership <em>programme</em> and is role-gated — an administrative act.
/// The register recorded joining against it and marked the capability served; it was not, and the
/// mark was hiding a real gap. This aggregate is a member's own relationship to a community, which
/// nothing modelled.
/// </para>
///
/// <para>
/// <b>Three refusal states, and they must stay distinct.</b> <see cref="CommunityMembershipState.Declined"/>
/// is the community saying no to a request. <see cref="CommunityMembershipState.Revoked"/> is the
/// community removing somebody who was in. <see cref="CommunityMembershipState.Left"/> is the
/// member's own decision. Collapsing any two of them puts the wrong sentence on somebody's screen —
/// "you were removed" where they had quietly left, or the reverse.
/// </para>
///
/// <para>
/// <b>A row is never deleted, and leaving does not erase having been there.</b> Somebody who leaves
/// and rejoins keeps one row through both, so the community can tell a returning member from a new
/// one, and so a revocation cannot be escaped by leaving and coming back. What that costs is that
/// <see cref="State"/> is the only truth about standing — the presence of a row means nothing on its
/// own, and every read has to say which states it counts.
/// </para>
///
/// <para>
/// <b>Primary lives here rather than on the member's profile.</b> The register asks for "setting and
/// clearing the primary community", and the constraint that matters is that a primary must be a
/// community the member actually belongs to. Holding the flag on the membership makes that
/// structural: there is nowhere to record a primary without a membership to hang it on.
/// </para>
/// </summary>
public sealed class CommunityMembership : AggregateRoot<CommunityMembershipId>, IAuditableEntity
{
    public const int ReasonMaximumLength = 500;

    /// <summary>
    /// Role names, stored as strings.
    /// <para>
    /// Names rather than numbers, for the reason every enum in this module is stored that way:
    /// reordering the enum would otherwise silently reassign every role on the platform.
    /// </para>
    /// </summary>
    private readonly List<string> _roles = [];

    private CommunityMembership(
        CommunityMembershipId id,
        CommunityId communityId,
        UserId memberId,
        CommunityMembershipState state)
        : base(id)
    {
        CommunityId = communityId;
        MemberId = memberId;
        State = state;
    }

    // EF Core.
    private CommunityMembership() { }

    public CommunityId CommunityId { get; private set; } = default!;

    public UserId MemberId { get; private set; } = default!;

    public CommunityMembershipState State { get; private set; }

    /// <summary>
    /// Whether this is the member's primary community. At most one membership per member may carry
    /// it, which the repository enforces — the aggregate cannot see the member's other rows.
    /// </summary>
    public bool IsPrimary { get; private set; }

    /// <summary>When they most recently became a member. Null until they are one.</summary>
    public DateTimeOffset? JoinedAt { get; private set; }

    /// <summary>
    /// When the member's connection to this community was verified. Set by <see cref="Verify"/>.
    /// </summary>
    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>
    /// Roles held here. Display only — authority is the capability set the server evaluates.
    /// <para>
    /// <see cref="CommunityMembershipRole.Member"/> is implied by <see cref="IsCurrentMember"/> and
    /// is not stored, so this list is empty for an ordinary member. Granting is a later increment;
    /// see <see cref="CommunityMembershipRole"/>.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<string> Roles => _roles.AsReadOnly();

    /// <summary>
    /// Who may see that this person belongs here.
    /// <para>
    /// <b>Private unless the member said otherwise, and it is never set by joining.</b> §41: the
    /// member opts in to being visible rather than out of it. The enum's zero is Private so a path
    /// that forgets this field fails closed.
    /// </para>
    /// </summary>
    public CommunityMembershipVisibility Visibility { get; private set; }

    /// <summary>When they most recently stopped being one, however that happened.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Why a request was declined or a membership revoked. Shown to the member.</summary>
    public string? DecisionReason { get; private set; }

    /// <summary>Who decided, where a decision was somebody's to make.</summary>
    public UserId? DecidedBy { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether this standing counts as belonging, for a count or a permission check.</summary>
    public bool IsCurrentMember =>
        State is CommunityMembershipState.Member or CommunityMembershipState.VerifiedMember;

    // ── Opening a relationship ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a relationship at a state that carries no standing — following a community's content,
    /// or saying you are interested before asking to join.
    /// </summary>
    public static Result<CommunityMembership> Start(
        CommunityMembershipId id,
        CommunityId communityId,
        UserId memberId,
        CommunityMembershipState state)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(communityId);
        ArgumentNullException.ThrowIfNull(memberId);

        if (state is not (CommunityMembershipState.Discovering
            or CommunityMembershipState.Interested
            or CommunityMembershipState.Following))
        {
            // Becoming a member goes through Join, which is where the community's own state and
            // approval rule are read. Nothing else may mint a Member row.
            return Result.Failure<CommunityMembership>(CommunityErrors.NotJoinable);
        }

        return Result.Success(new CommunityMembership(id, communityId, memberId, state));
    }

    /// <summary>
    /// The founder's membership, minted at the moment the community is proposed.
    /// <para>
    /// <b>The second way to mint a Member row, and the only other one.</b> <see cref="Join"/> reads
    /// the community's own rules and refuses a community that is not open — and a proposed community
    /// is not open until staff open it. The person who proposed it is not asking to join; they are
    /// the reason it exists, and they belong to it from the first moment or the Owner role has
    /// nobody to sit on.
    /// </para>
    /// <para>
    /// <b>Visible to the community, not private.</b> §41 makes a member's belonging private until
    /// they choose otherwise, and that default is right for joining. Founding is a different act and
    /// an explicit, public one; an owner the members cannot see is an owner they cannot contact or
    /// hold to account. The founder may still change it — it is their membership.
    /// </para>
    /// </summary>
    public static Result<CommunityMembership> Found(
        CommunityMembershipId id,
        Community community,
        UserId founder,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(community);
        ArgumentNullException.ThrowIfNull(founder);

        if (community.Status == CommunityStatus.Archived)
        {
            return Result.Failure<CommunityMembership>(CommunityErrors.NotJoinable);
        }

        var membership = new CommunityMembership(
            id, community.Id, founder, CommunityMembershipState.Discovering);

        membership.Become(CommunityMembershipState.Member, now);
        membership.Visibility = CommunityMembershipVisibility.CommunityOnly;
        membership._roles.Add(nameof(CommunityMembershipRole.Owner));
        membership._roles.Add(nameof(CommunityMembershipRole.Administrator));

        return Result.Success(membership);
    }

    /// <summary>
    /// Asks to join. The community decides whether that is immediate or a request.
    /// <para>
    /// <paramref name="community"/> is passed rather than looked up so the decision reads in one
    /// place: whether it is open, whether an invitation is needed, and whether approval is. A
    /// revoked member cannot rejoin by asking — that is the point of keeping the row.
    /// </para>
    /// </summary>
    /// <summary>
    /// Moves an existing row back to a state that carries no standing — following, or interested.
    /// <para>
    /// <b>The counterpart to <see cref="Start"/> for a row that already exists.</b> Somebody who
    /// left may follow; somebody who was declined may follow while they wait to ask again. Neither
    /// is membership and neither touches the community's count.
    /// </para>
    /// <para>
    /// <b>Refused from every state that carries standing, and from <see cref="CommunityMembershipState.Revoked"/>.</b>
    /// A member must not be quietly demoted to a follower, somebody waiting on approval must not
    /// lose their place in the queue, and a revocation must not be laundered by following.
    /// </para>
    /// </summary>
    public Result Restart(CommunityMembershipState state)
    {
        if (state is not (CommunityMembershipState.Discovering
            or CommunityMembershipState.Interested
            or CommunityMembershipState.Following))
        {
            return Result.Failure(CommunityErrors.NotJoinable);
        }

        if (State == CommunityMembershipState.Revoked)
        {
            return Result.Failure(CommunityErrors.MembershipRevoked);
        }

        if (IsCurrentMember || State == CommunityMembershipState.PendingApproval)
        {
            return Result.Failure(CommunityErrors.AlreadyMember);
        }

        State = state;

        // The row is no longer in a refusal state, so it must stop carrying a refusal's reason —
        // the client renders stateReason verbatim, and "you were declined" under a Following badge
        // is a sentence nobody meant to write.
        EndedAt = null;
        DecisionReason = null;
        DecidedBy = null;
        Revision++;

        return Result.Success();
    }

    public Result Join(Community community, bool requiresApproval, DateTimeOffset now, bool hasInvitation = false)
    {
        ArgumentNullException.ThrowIfNull(community);

        if (!community.IsJoinable)
        {
            return Result.Failure(CommunityErrors.NotJoinable);
        }

        if (State == CommunityMembershipState.Revoked)
        {
            return Result.Failure(CommunityErrors.MembershipRevoked);
        }

        if (IsCurrentMember)
        {
            return Result.Failure(CommunityErrors.AlreadyMember);
        }

        if (community.Visibility == CommunityVisibility.InviteOnly && !hasInvitation)
        {
            return Result.Failure(CommunityErrors.InviteRequired);
        }

        if (requiresApproval)
        {
            State = CommunityMembershipState.PendingApproval;
            DecisionReason = null;
            return Result.Success();
        }

        Become(CommunityMembershipState.Member, now);
        RaiseJoined(CommunityJoinRoute.Direct, now);
        return Result.Success();
    }

    /// <summary>Approves a pending request.</summary>
    public Result Approve(UserId decidedBy, DateTimeOffset now)
    {
        if (State != CommunityMembershipState.PendingApproval)
        {
            return Result.Failure(CommunityErrors.NotAMember);
        }

        DecidedBy = decidedBy;
        Become(CommunityMembershipState.Member, now);
        RaiseJoined(CommunityJoinRoute.Approved, now);
        return Result.Success();
    }

    /// <summary>Refuses a pending request. The reason reaches the member.</summary>
    public Result Decline(UserId decidedBy, string? reason)
    {
        if (State != CommunityMembershipState.PendingApproval)
        {
            return Result.Failure(CommunityErrors.NotAMember);
        }

        State = CommunityMembershipState.Declined;
        DecidedBy = decidedBy;
        DecisionReason = Clip(reason);
        return Result.Success();
    }

    /// <summary>
    /// Marks a member's connection to the community as verified. Never a first step: verification is
    /// a statement about somebody who already belongs.
    /// </summary>
    public Result Verify(DateTimeOffset? now = null)
    {
        if (State != CommunityMembershipState.Member)
        {
            return Result.Failure(CommunityErrors.NotAMember);
        }

        State = CommunityMembershipState.VerifiedMember;

        // The client shows "verified member since"; a verified state with no date cannot say it.
        VerifiedAt = now ?? DateTimeOffset.UtcNow;
        return Result.Success();
    }

    // ── Ending a relationship ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The member's own decision. Clears primary, because a primary community you have left is a
    /// claim on your profile you did not make.
    /// </summary>
    public Result Leave(DateTimeOffset now)
    {
        if (!IsCurrentMember && State != CommunityMembershipState.PendingApproval)
        {
            return Result.Failure(CommunityErrors.NotAMember);
        }

        // **The owner is the one member a community cannot lose by accident.** Ending a membership
        // clears every role, so an owner who left took the community's Owner with them, and — as its
        // only administrator, which every founder starts as — the last person who could appoint
        // anyone. A community still pending has nobody else in it at all, so not even Diaspora's
        // appoint-an-owner route has a member to appoint. Found on the device on 11 Sep 2026, where
        // the founder's own community page offered "Leave" beside the owner badge. Handing a
        // community to somebody else is a transfer, and a separate act.
        if (Holds(CommunityMembershipRole.Owner))
        {
            return Result.Failure(CommunityErrors.OwnerCannotLeave);
        }

        End(CommunityMembershipState.Left, now, reason: null, decidedBy: null);
        return Result.Success();
    }

    /// <summary>The community's decision. A revoked member cannot rejoin by asking.</summary>
    public Result Revoke(UserId decidedBy, string? reason, DateTimeOffset now)
    {
        if (!IsCurrentMember)
        {
            return Result.Failure(CommunityErrors.NotAMember);
        }

        // Nothing calls this yet. When something does, removing the owner would orphan the community
        // exactly as the owner leaving would — see `Leave` — so it is refused here, where no future
        // caller can skip it.
        if (Holds(CommunityMembershipRole.Owner))
        {
            return Result.Failure(CommunityErrors.OwnerCannotBeRemoved);
        }

        End(CommunityMembershipState.Revoked, now, reason, decidedBy);
        return Result.Success();
    }

    /// <summary>Stops following without ever having been a member.</summary>
    public Result Unfollow()
    {
        // Following and Interested both, because the client's `unfollow` and `remove-interest` are
        // the same transition: stepping back from a relationship that never carried standing. A
        // member cannot reach it — leaving a community is Leave, and it records a departure.
        if (State is not (CommunityMembershipState.Following or CommunityMembershipState.Interested))
        {
            return Result.Failure(CommunityErrors.NotAMember);
        }

        State = CommunityMembershipState.None;
        Revision++;
        return Result.Success();
    }

    // ── Roles and visibility ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Grants a role. Granting one already held is not an error — it is the state asked for.
    /// <para>
    /// Refused for anybody who does not currently belong: a moderator who is not a member is a
    /// contradiction, and the check belongs here rather than at every future call site.
    /// </para>
    /// </summary>
    public Result Grant(CommunityMembershipRole role)
    {
        if (!IsCurrentMember)
        {
            return Result.Failure(CommunityErrors.NotAMember);
        }

        // A role is an office in the community, and holding one shows other members that you
        // belong. Somebody who kept their membership private made a choice §41 protects, and giving
        // them a role would undo it without asking. So the member makes themselves visible first,
        // and the role follows. Checked here, not at the route, so no later caller can skip it.
        if (role != CommunityMembershipRole.Member && Visibility == CommunityMembershipVisibility.Private)
        {
            return Result.Failure(CommunityErrors.RoleNeedsVisibleMembership);
        }

        var name = role.ToString();

        if (!_roles.Contains(name, StringComparer.Ordinal))
        {
            _roles.Add(name);
        }

        return Result.Success();
    }

    /// <summary>
    /// Removes a role. Removing one not held is what was asked for, not an error.
    /// <para>
    /// <b>Refused for the Owner's own standing.</b> The Owner role is not removed here at all — that
    /// is a transfer, and a separate act. And the Owner keeps Administrator, because an owner who
    /// cannot administer their own community is a state nobody could get out of.
    /// </para>
    /// </summary>
    public Result RevokeRole(CommunityMembershipRole role)
    {
        if (role == CommunityMembershipRole.Owner)
        {
            return Result.Failure(CommunityErrors.OwnerRoleNotAssignable);
        }

        if (role == CommunityMembershipRole.Administrator && Holds(CommunityMembershipRole.Owner))
        {
            return Result.Failure(CommunityErrors.OwnerKeepsAdministration);
        }

        _roles.RemoveAll(held => string.Equals(held, role.ToString(), StringComparison.Ordinal));
        Revision++;
        return Result.Success();
    }

    /// <summary>
    /// Makes this member the Owner of a community that has none — a staff act.
    /// <para>
    /// <b>Why this exists at all.</b> The founding communities were put into the directory by a
    /// seeder, not by a person, so "whoever creates it owns it" gives them nobody. These are the
    /// communities launch actually looks like, and without this the rule would reach every community
    /// except the ones that matter most. The caller checks that the community has no owner; this
    /// checks that the person can hold the office.
    /// </para>
    /// </summary>
    public Result AppointOwner()
    {
        if (!IsCurrentMember)
        {
            return Result.Failure(CommunityErrors.NotAMember);
        }

        if (Visibility == CommunityMembershipVisibility.Private)
        {
            return Result.Failure(CommunityErrors.RoleNeedsVisibleMembership);
        }

        foreach (var role in new[] { CommunityMembershipRole.Owner, CommunityMembershipRole.Administrator })
        {
            if (!Holds(role))
            {
                _roles.Add(role.ToString());
            }
        }

        Revision++;
        return Result.Success();
    }

    /// <summary>
    /// Hands the office on — 11 Sep 2026.
    /// <para>
    /// The Owner role goes and Administrator stays: the person who founded a community usually knows
    /// best how it is run, and the new owner can remove them if that is not wanted. Being no longer
    /// the owner, they may also leave now, which as owner they could not.
    /// </para>
    /// <para>
    /// Reachable only from an accepted <see cref="CommunityOwnershipTransfer"/>. There is no route
    /// that gives up ownership into thin air: a community with no owner is one nobody can appoint
    /// anybody in, and the only cure is a Diaspora administrator.
    /// </para>
    /// </summary>
    public Result StepDownAsOwner()
    {
        if (!Holds(CommunityMembershipRole.Owner))
        {
            return Result.Failure(CommunityErrors.NotTheOwner);
        }

        _roles.RemoveAll(held =>
            string.Equals(held, CommunityMembershipRole.Owner.ToString(), StringComparison.Ordinal));

        if (!Holds(CommunityMembershipRole.Administrator))
        {
            _roles.Add(CommunityMembershipRole.Administrator.ToString());
        }

        Revision++;
        return Result.Success();
    }

    /// <summary>Whether this membership holds a role.</summary>
    public bool Holds(CommunityMembershipRole role) =>
        _roles.Contains(role.ToString(), StringComparer.Ordinal);

    /// <summary>
    /// Sets who may see this membership. The member's own choice, and nobody else's.
    /// </summary>
    public void SetVisibility(CommunityMembershipVisibility visibility) => Visibility = visibility;

    // ── Primary ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Makes this the member's primary community. Refused unless they currently belong — otherwise
    /// a profile could name a community the person has no standing in.
    /// </summary>
    public Result MakePrimary()
    {
        if (!IsCurrentMember)
        {
            return Result.Failure(CommunityErrors.PrimaryRequiresMembership);
        }

        IsPrimary = true;
        return Result.Success();
    }

    /// <summary>Clears the flag. Never an error: clearing what is already clear is what was asked.</summary>
    public void ClearPrimary() => IsPrimary = false;

    // ── Audit ───────────────────────────────────────────────────────────────────────────────

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private void Become(CommunityMembershipState state, DateTimeOffset now)
    {
        State = state;
        JoinedAt = now;
        EndedAt = null;
        DecisionReason = null;
        Revision++;
    }

    /// <summary>
    /// Announces a transition into membership — 21 Oct 2026, for <c>community.memberJoined</c>.
    /// <para>
    /// Called from the two places a request becomes membership, after the state has moved, so the
    /// event can never describe a join that did not happen. <see cref="Found"/> does not call it.
    /// </para>
    /// </summary>
    private void RaiseJoined(CommunityJoinRoute route, DateTimeOffset now) =>
        RaiseDomainEvent(new CommunityMemberJoinedDomainEvent
        {
            MembershipId = Id,
            CommunityId = CommunityId,
            MemberId = MemberId,
            JoinedAt = now,
            Route = route,
            Visibility = Visibility
        });

    private void End(CommunityMembershipState state, DateTimeOffset now, string? reason, UserId? decidedBy)
    {
        State = state;
        EndedAt = now;
        DecisionReason = Clip(reason);
        DecidedBy = decidedBy;

        // A community you no longer belong to cannot be the one your profile leads with.
        IsPrimary = false;

        // Nor can you moderate it. A revoked moderator who kept the badge would keep it on every
        // screen that reads roles for display, which is every screen that shows one.
        _roles.Clear();

        Revision++;
    }

    private static string? Clip(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= ReasonMaximumLength ? trimmed : trimmed[..ReasonMaximumLength];
    }
}
