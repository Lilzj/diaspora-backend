namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Publishes, pauses, closes or archives a provider. The owner's route.</summary>
public sealed record ProviderStatusRequest
{
    /// <summary>
    /// The <c>ProviderStatus</c> to move to: <c>Active</c>, <c>Paused</c>, <c>Closed</c> or
    /// <c>Archived</c>. <c>Suspended</c>, and <c>Active</c> on a suspended provider, are refused
    /// here for everybody; staff suspend and reinstate through
    /// <c>POST /providers/moderation/{providerId}</c>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Why, in the owner's words. Optional; shown to the owner's team.</summary>
    public string? Reason { get; init; }
}
