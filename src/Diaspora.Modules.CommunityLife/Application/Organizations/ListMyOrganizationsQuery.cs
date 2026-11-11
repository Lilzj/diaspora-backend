using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// The bodies the caller runs or belongs to, drafts included.
/// <para>
/// Separate from the directory because it is the only read that legitimately returns unpublished
/// pages — and it returns them only to people who are already inside.
/// </para>
/// </summary>
[RequireAuthentication("The caller's own organizations, scoped from the token.")]
public sealed record ListMyOrganizationsQuery : IQuery<OrganizationSearchResultResponse>
{
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
