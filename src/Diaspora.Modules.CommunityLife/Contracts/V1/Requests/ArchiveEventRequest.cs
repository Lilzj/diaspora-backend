namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>File an event away.</summary>
public sealed record ArchiveEventRequest
{
    public string? Reason { get; init; }
}
