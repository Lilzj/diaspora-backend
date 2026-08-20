namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>Takes a listing down.</summary>
public sealed record ArchiveListingRequest
{
    /// <summary>
    /// Why. Shown to anybody mid-negotiation, who is owed an explanation rather than a listing
    /// that silently stops answering.
    /// </summary>
    public string? Reason { get; init; }
}
