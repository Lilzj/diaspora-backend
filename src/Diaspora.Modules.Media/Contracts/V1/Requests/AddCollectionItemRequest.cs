namespace Diaspora.Modules.Media.Contracts.V1.Requests;

/// <summary>Places one asset in a collection, at the end.</summary>
public sealed record AddCollectionItemRequest
{
    public required Guid MediaId { get; init; }

    /// <summary>Caption for this placement. The same asset can be captioned differently elsewhere.</summary>
    public string? Caption { get; init; }
}
