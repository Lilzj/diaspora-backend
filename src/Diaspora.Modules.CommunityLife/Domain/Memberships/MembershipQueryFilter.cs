using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Domain.Memberships;

/// <summary>
/// What a membership page is being asked for.
/// <para>
/// A record rather than a wall of optional repository parameters, for the reason B7 first recorded:
/// a repository method with eleven nullable arguments is a method every caller passes nulls into
/// positionally, and the first one that transposes two of them fails silently.
/// </para>
/// </summary>
public sealed record MembershipQueryFilter
{
    public OrganizationId? OrganizationId { get; init; }

    public UserId? MemberUserId { get; init; }

    public IReadOnlyList<MembershipStatus>? Statuses { get; init; }

    public MembershipRole? MinimumRole { get; init; }

    /// <summary>Free text over the member's cached display name, when the projection carries one.</summary>
    public string? SearchTerm { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
