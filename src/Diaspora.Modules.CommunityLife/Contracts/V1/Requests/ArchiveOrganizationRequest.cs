namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Take an organization off the directory.</summary>
public sealed record ArchiveOrganizationRequest
{
    public string? Reason { get; init; }
}
