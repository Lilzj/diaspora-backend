using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Users;

/// <summary>
/// The member's platform role.
/// <para>
/// Deliberately a domain type rather than a reuse of <c>Diaspora.Contracts.Enums.UserRole</c>:
/// Contracts is an outward-facing cross-module surface, and the domain must not depend on
/// it. The Application layer maps between the two at the boundary.
/// </para>
/// <para>
/// Roles never gate behaviour directly. They resolve to permissions, and permissions are
/// what authorisation checks against — see the permission resolver in the Application layer.
/// </para>
/// </summary>
public sealed class UserRole : Enumeration
{
    public static readonly UserRole Guest = new(0, nameof(Guest), isStaff: false, rank: 0);
    public static readonly UserRole CommunityUser = new(1, nameof(CommunityUser), isStaff: false, rank: 0);
    public static readonly UserRole ServiceProvider = new(2, nameof(ServiceProvider), isStaff: false, rank: 0);
    public static readonly UserRole Moderator = new(3, nameof(Moderator), isStaff: true, rank: 1);
    public static readonly UserRole Admin = new(4, nameof(Admin), isStaff: true, rank: 2);
    public static readonly UserRole SuperAdmin = new(5, nameof(SuperAdmin), isStaff: true, rank: 3);

    public bool IsStaff { get; }

    /// <summary>
    /// Where the role stands for acting on somebody else's account: member &lt; Moderator &lt; Admin
    /// &lt; SuperAdmin. Every role that is not staff is a member here.
    /// <para>
    /// The owner's rule (7 Nov 2026): staff act only on accounts strictly below their own rank. A
    /// Moderator could suspend an Admin, and anyone could act on themselves, until then.
    /// </para>
    /// </summary>
    public int Rank { get; }

    private UserRole(int id, string name, bool isStaff, int rank) : base(id, name)
    {
        IsStaff = isStaff;
        Rank = rank;
    }

    /// <summary>Whether this role stands strictly above <paramref name="other"/>.</summary>
    public bool Outranks(UserRole other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Rank > other.Rank;
    }

    public static IReadOnlyCollection<UserRole> All => GetAll<UserRole>().ToArray();
}
