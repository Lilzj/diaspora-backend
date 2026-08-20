namespace Diaspora.Modules.CommunityLife.Integration;

/// <summary>
/// One member's standing in one organization, as another module sees it.
/// <para>
/// <b>B18 added this, and the reason it did not exist before is worth recording.</b>
/// <see cref="IOrganizationDirectory.CanActOnBehalfAsync"/> has always answered the yes/no
/// authorisation question, which is all any consumer needed until a module had to <em>render</em>
/// the answer: "which organizations am I in, and as what" is a screen, and a screen cannot be drawn
/// from a boolean asked six times. This is the same edge, read out rather than compared against.
/// </para>
/// <para>
/// <b>Flat primitives and strings, exactly as <see cref="OrganizationSummary"/> is.</b> The role is
/// its name rather than B12's enum, so a consumer references nothing of this module's and a refactor
/// inside the aggregate cannot change the contract.
/// </para>
/// <para>
/// <b>Current memberships only.</b> A membership that ended grants nothing, and a consumer that had
/// to filter on <see cref="Status"/> to avoid granting on one would eventually forget. The status is
/// carried for display, not for the caller to make the decision with.
/// </para>
/// </summary>
public sealed record OrganizationMembershipSummary
{
    /// <summary>B12's membership row. What the role-change route names.</summary>
    public required Guid MembershipId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid MemberUserId { get; init; }

    /// <summary><c>Member</c>, <c>Volunteer</c>, <c>Moderator</c>, <c>Organizer</c>, <c>Administrator</c>, <c>Owner</c>.</summary>
    public required string Role { get; init; }

    /// <summary>B12's own membership status name. Display only — see the type summary.</summary>
    public required string Status { get; init; }

    public DateTimeOffset? JoinedAt { get; init; }
}
