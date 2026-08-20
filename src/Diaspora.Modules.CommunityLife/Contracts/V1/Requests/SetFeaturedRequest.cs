namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Promote or demote an event or an organization. Staff only.</summary>
public sealed record SetFeaturedRequest
{
    public required bool IsFeatured { get; init; }
}
