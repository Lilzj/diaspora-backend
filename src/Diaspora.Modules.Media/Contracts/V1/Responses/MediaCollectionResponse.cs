namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// An ordered set of assets: a gallery, an attachment set, a provider's four photographs.
/// <para>
/// <see cref="Items"/> arrives in render order, so a client's existing <c>.map()</c> over
/// <c>gallery[]</c> needs no change. The difference from what the client does today is that
/// the order survives a refetch, reaches a second device, and is what the member actually
/// arranged.
/// </para>
/// </summary>
public sealed record MediaCollectionResponse
{
    public required Guid CollectionId { get; init; }

    /// <summary><c>gallery</c>, <c>providerGallery</c>, <c>messageAttachments</c>, and so on.</summary>
    public required string Kind { get; init; }

    public required string OwnerType { get; init; }

    public required Guid OwnerId { get; init; }

    public string? Name { get; init; }

    /// <summary>How many assets this kind of collection may hold.</summary>
    public required int Capacity { get; init; }

    /// <summary>In render order.</summary>
    public required IReadOnlyList<MediaCollectionItemResponse> Items { get; init; }
}
