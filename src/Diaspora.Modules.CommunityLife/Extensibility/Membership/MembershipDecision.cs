using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Extensibility.Membership;

/// <summary>
/// What a membership policy provider decided.
/// <para>
/// Three outcomes rather than a boolean: admit, queue for a human, refuse. Collapsing "queue" into
/// either of the others is what turns an approval workflow into either a rubber stamp or a wall.
/// </para>
/// </summary>
public sealed record MembershipDecision
{
    /// <summary>The status the membership should open in.</summary>
    public required MembershipStatus Status { get; init; }

    /// <summary>Why, when the answer is a refusal. Shown to the applicant.</summary>
    public string? Reason { get; init; }

    /// <summary>The role to grant on admission. Almost always Member.</summary>
    public MembershipRole Role { get; init; } = MembershipRole.Member;

    public static MembershipDecision Admit(MembershipRole role = MembershipRole.Member) =>
        new() { Status = MembershipStatus.Active, Role = role };

    public static MembershipDecision Queue() =>
        new() { Status = MembershipStatus.Requested };

    public static MembershipDecision Refuse(string reason) =>
        new() { Status = MembershipStatus.Rejected, Reason = reason };
}
