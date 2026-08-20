namespace Diaspora.Modules.Media.Contracts.V1.Requests;

/// <summary>Creates a gallery, an attachment set, or any other ordered group of assets.</summary>
public sealed record CreateCollectionRequest
{
    /// <summary>
    /// <c>gallery</c>, <c>providerGallery</c>, <c>messageAttachments</c>,
    /// <c>verificationEvidence</c>, <c>articleImages</c>, <c>locationPhotos</c>,
    /// <c>general</c>.
    /// <para>
    /// The kind carries the cardinality rule — four for a provider gallery because the wizard
    /// renders four slots, five for message attachments because the picker's
    /// <c>selectionLimit</c> is five.
    /// </para>
    /// </summary>
    public required string Kind { get; init; }

    public required string OwnerType { get; init; }

    public required Guid OwnerId { get; init; }

    public string? Name { get; init; }

    /// <summary>Assets to place, in order. May be empty.</summary>
    public IReadOnlyList<Guid> MediaIds { get; init; } = [];
}
