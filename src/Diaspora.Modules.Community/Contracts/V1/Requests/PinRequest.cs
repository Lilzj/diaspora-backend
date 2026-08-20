namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>Pinning or unpinning. A pinned post is still published; pinning is a flag.</summary>
public sealed record PinRequest
{
    public required bool Pinned { get; init; }
}
