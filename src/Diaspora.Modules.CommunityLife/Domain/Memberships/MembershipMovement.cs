namespace Diaspora.Modules.CommunityLife.Domain.Memberships;

/// <summary>
/// The change one membership makes to an organization's four member counters.
/// <para>
/// <b>Two different events move the same numbers.</b> A status change and a role change both alter
/// how many administrators and how many volunteers a body has — somebody promoted to administrator
/// and somebody admitted as one are the same movement — so both handlers compute it here rather than
/// each keeping their own arithmetic. The event counters are not in this shape because only the
/// event handlers touch them and nothing else can disagree about them.
/// </para>
/// </summary>
public sealed record MembershipMovement(
    int Members, int Pending, int Administrators, int Volunteers)
{
    public static MembershipMovement None { get; } = new(0, 0, 0, 0);

    /// <summary>Whether the movement is worth a write at all.</summary>
    public bool IsEmpty =>
        Members == 0 && Pending == 0 && Administrators == 0 && Volunteers == 0;
}
