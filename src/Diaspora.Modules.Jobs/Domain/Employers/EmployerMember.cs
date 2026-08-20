using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Domain.Employers;

/// <summary>
/// One member's place inside an employer.
/// <para>
/// <b>This is the module's real authorisation primitive.</b> The platform's permission
/// catalogue answers "may this person recruit at all"; this answers "may this person recruit
/// <em>here</em>", and only the employer aggregate can. A member may own one employer, recruit
/// at another and have nothing to do with a third, and none of that fits on a token.
/// </para>
/// <para>
/// The pair (employer, user) is the identity, which is what makes adding a member idempotent.
/// </para>
/// </summary>
public sealed class EmployerMember
{
    private EmployerMember(UserId userId, EmployerMemberRole role, DateTimeOffset joinedAt)
    {
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    private EmployerMember() { }

    /// <summary>Identity's user identifier. A value, not a foreign key.</summary>
    public UserId UserId { get; private set; } = default!;

    public EmployerMemberRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>Whether this member holds at least the authority asked for.</summary>
    public bool HasAtLeast(EmployerMemberRole required) => Role >= required;

    internal static EmployerMember Create(
        UserId userId, EmployerMemberRole role, DateTimeOffset joinedAt) =>
        new(userId, role, joinedAt);

    internal void ChangeRole(EmployerMemberRole role) => Role = role;
}
