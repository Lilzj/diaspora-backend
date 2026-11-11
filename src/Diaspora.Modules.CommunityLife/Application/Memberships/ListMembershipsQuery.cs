using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>
/// An organization's members.
/// <para>
/// Visible to members where the body publishes its list, and to administrators always. A closed
/// list is closed because who belongs to a support group or a political association is not a fact
/// anybody outside it should be able to read.
/// </para>
/// </summary>
[RequireAuthentication(
    "Authentication is all the gate can ask. A body's member list is visible to members where it " +
    "publishes one and to administrators always, which is a fact about the organization rather " +
    "than about the caller's permissions.")]
public sealed record ListMembershipsQuery : IQuery<MembershipPageResponse>
{
    public required Guid OrganizationId { get; init; }

    public IReadOnlyList<string>? Statuses { get; init; }

    public string? MinimumRole { get; init; }

    public string? SearchTerm { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
