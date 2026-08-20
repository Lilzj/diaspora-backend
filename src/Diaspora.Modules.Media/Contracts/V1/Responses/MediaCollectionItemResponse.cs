namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// One asset's placement in a collection.
/// <para>
/// Carries the whole asset rather than only its identifier, because every caller that reads a
/// collection is about to render it — a gallery response that returned bare identifiers would
/// be immediately followed by a request per photograph.
/// </para>
/// </summary>
public sealed record MediaCollectionItemResponse
{
    public required Guid MediaId { get; init; }

    public required int Position { get; init; }

    /// <summary>Scoped to this placement: the same photograph can be captioned twice.</summary>
    public string? Caption { get; init; }

    public required MediaAssetResponse Media { get; init; }
}
