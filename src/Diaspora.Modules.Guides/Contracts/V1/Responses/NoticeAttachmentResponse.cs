namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One media reference on a notice.
/// <para>
/// <b>An identifier and a caption, never a URL.</b> ADR-005 again: the client resolves the media
/// identifier through B5, which is what lets a deployment move storage, revoke access or put delivery
/// behind a signed URL without every notice ever published becoming wrong.
/// </para>
/// </summary>
public sealed record NoticeAttachmentResponse
{
    public required Guid MediaId { get; init; }

    /// <summary>What the asset is, in words. The accessible description.</summary>
    public string? Caption { get; init; }

    public required int Position { get; init; }
}
