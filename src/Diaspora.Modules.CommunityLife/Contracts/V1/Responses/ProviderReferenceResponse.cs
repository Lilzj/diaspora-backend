namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A B7 provider involved in an event.</summary>
public sealed record ProviderReferenceResponse
{
    public required Guid ProviderId { get; init; }

    public string? ProviderName { get; init; }

    /// <summary>What they are doing: Host, Venue, Caterer, Sponsor.</summary>
    public string? Role { get; init; }
}
