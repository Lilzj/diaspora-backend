using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// Opens an asset's bytes for reading.
/// <para>
/// Carries the signed-token fields because this is the one endpoint an <c>&lt;img src&gt;</c>
/// hits directly, and an image tag cannot send an <c>Authorization</c> header. When the caller
/// is authenticated the token is unnecessary; when the request comes from a rendered page it
/// is the only credential available.
/// </para>
/// </summary>
public sealed record GetMediaContentQuery : IQuery<MediaContentResult>
{
    public required Guid MediaId { get; init; }

    /// <summary>Which rendition. Defaults to the original.</summary>
    public string? Variant { get; init; }

    /// <summary>Signature from a minted link, when the caller is presenting one.</summary>
    public string? Signature { get; init; }

    public long? ExpiresAtUnixSeconds { get; init; }

    public Guid? AudienceUserId { get; init; }

    /// <summary>Byte offset for a range request. Zero for the whole object.</summary>
    public long RangeOffset { get; init; }

    /// <summary>Length for a range request. Null for "to the end".</summary>
    public long? RangeLength { get; init; }
}
