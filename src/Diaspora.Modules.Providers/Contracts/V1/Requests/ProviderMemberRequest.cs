namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Adds somebody to a provider, or changes what they may do.</summary>
public sealed record ProviderMemberRequest
{
    public required Guid UserId { get; init; }

    /// <summary>Lower-case, matching the client's <c>OrganizationRole</c> union.</summary>
    public required string Role { get; init; }

    /// <summary>The provider's own label. "Head Pastor", "Practice Manager".</summary>
    public string? Title { get; init; }

    /// <summary>Whether to show this person on the public profile.</summary>
    public bool IsPubliclyListed { get; init; }
}
