using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Queries.Administration.FindMember;

/// <summary>
/// Finds one member by something staff already have: an identifier, an email address or a
/// phone number.
/// <para>
/// <b>A lookup, not a search.</b> There is no free-text hunt across the membership here,
/// and that is the point: staff reach for this when somebody has written in, or a report
/// names an account, so an exact handle is what they hold. A "find me everyone called
/// Amara" over a diaspora platform is a different feature with different consequences, and
/// it should be asked for deliberately rather than arrive as a side effect of this.
/// </para>
/// </summary>
[Auditable(Action = "identity.member.looked-up", OnlyForStaff = true)]
[Authorize(Permission = IdentityPermissions.ReadAnyUser)]
public sealed record FindMemberQuery : IQuery<AdminMemberResponse>
{
    /// <summary>An account identifier, an email address, or a phone number in any format.</summary>
    public required string Term { get; init; }
}
