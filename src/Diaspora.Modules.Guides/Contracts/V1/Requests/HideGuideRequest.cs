namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Removes the guide from every public surface, reversibly. The reason is required — a
/// moderation action nobody can explain six months later is an action that should not have
/// been possible.
/// </summary>
public sealed record HideGuideRequest
{
    public required string Reason { get; init; }
}
