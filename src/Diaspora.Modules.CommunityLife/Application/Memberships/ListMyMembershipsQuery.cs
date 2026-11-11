using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>The caller's own memberships, pending ones included.</summary>
[RequireAuthentication("The caller's own memberships, scoped from the token.")]
public sealed record ListMyMembershipsQuery : IQuery<MembershipPageResponse>
{
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
