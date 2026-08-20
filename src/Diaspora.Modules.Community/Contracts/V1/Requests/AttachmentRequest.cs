namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>
/// One attachment on a post being created or edited.
/// <para>
/// Carries a media identifier, never a URL. A client uploads through Media, receives an
/// identifier, and sends it here — which is the whole of ADR-005 as it appears on the wire.
/// </para>
/// </summary>
public sealed record AttachmentRequest
{
    /// <summary><c>image</c>, <c>video</c>, <c>document</c>, <c>audio</c> or <c>link</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Required for every kind but <c>link</c>.</summary>
    public Guid? MediaId { get; init; }

    /// <summary>Required for <c>link</c>. Stored as text and never fetched by the server.</summary>
    public string? Url { get; init; }

    /// <summary>A Media reference role. Defaults to <c>gallery</c> for images.</summary>
    public string? Role { get; init; }

    public string? Caption { get; init; }
}
