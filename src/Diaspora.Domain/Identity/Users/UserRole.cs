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
    public static readonly UserRole Guest = new(0, nameof(Guest), isStaff: false);
    public static readonly UserRole CommunityUser = new(1, nameof(CommunityUser), isStaff: false);
    public static readonly UserRole ServiceProvider = new(2, nameof(ServiceProvider), isStaff: false);
    public static readonly UserRole Moderator = new(3, nameof(Moderator), isStaff: true);
    public static readonly UserRole Admin = new(4, nameof(Admin), isStaff: true);
    public static readonly UserRole SuperAdmin = new(5, nameof(SuperAdmin), isStaff: true);

    public bool IsStaff { get; }

    private UserRole(int id, string name, bool isStaff) : base(id, name)
    {
        IsStaff = isStaff;
    }

    public static IReadOnlyCollection<UserRole> All => GetAll<UserRole>().ToArray();
}
