namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>One Media asset to attach, by reference.</summary>
public sealed record MediaAttachmentRequest
{
    public required Guid MediaId { get; init; }

    /// <summary>
    /// <c>Cover</c>, <c>Logo</c>, <c>Gallery</c>, <c>Video</c>, <c>Brochure</c>, <c>Document</c>,
    /// <c>SpeakerPhoto</c>. Unrecognised values fall back to <c>Gallery</c> rather than failing the
    /// write: a client on an older build should not be unable to add a photograph.
    /// </summary>
    public string? Role { get; init; }

    public string? Caption { get; init; }
}
