namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Publishes, pauses, closes, suspends, reinstates or archives a provider.</summary>
public sealed record ProviderStatusRequest
{
    /// <summary>
    /// The <c>ProviderStatus</c> to move to. Not every transition is reachable by every caller:
    /// <c>Suspended</c> is staff-only, and <c>Closed</c> is the owner's.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Why. Required for a suspension; shown to the owner.</summary>
    public string? Reason { get; init; }
}
