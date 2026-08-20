namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Replace an organization's leadership list wholesale.</summary>
public sealed record SetOrganizationLeadersRequest
{
    public int? BaseRevision { get; init; }

    public IReadOnlyList<OrganizationLeaderRequest> Leaders { get; init; } = [];
}
