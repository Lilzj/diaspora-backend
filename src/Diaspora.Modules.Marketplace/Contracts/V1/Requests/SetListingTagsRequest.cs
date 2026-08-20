namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>Replaces a listing's tags wholesale — that is how the chip editor saves.</summary>
public sealed record SetListingTagsRequest
{
    public required IReadOnlyList<string> Tags { get; init; }

    public int? BaseRevision { get; init; }
}
