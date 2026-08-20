namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Hands a provider to somebody else.</summary>
public sealed record TransferOwnershipRequest
{
    public required Guid NewOwnerId { get; init; }

    /// <summary>
    /// Whether the outgoing owner stays on as a manager. True for a retiring founder, false for
    /// a sale — two different events, and the notification and audit trail differ.
    /// </summary>
    public bool RetainPreviousOwner { get; init; } = true;
}
