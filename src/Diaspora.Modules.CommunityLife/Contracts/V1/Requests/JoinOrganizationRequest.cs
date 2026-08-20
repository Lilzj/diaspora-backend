namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Ask to join an organization, or accept an invitation.</summary>
public sealed record JoinOrganizationRequest
{
    /// <summary>What to tell the administrators. Read by whoever decides, and cleared afterwards.</summary>
    public string? Message { get; init; }
}
