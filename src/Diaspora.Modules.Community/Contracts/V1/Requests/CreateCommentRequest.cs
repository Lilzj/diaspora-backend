namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>
/// A comment on anything.
/// <para>
/// Content type and identifier are on the body rather than only in the route, because the
/// client's own comment model carries them and because the same endpoint serves a comment on a
/// guide, an event and a post.
/// </para>
/// </summary>
public sealed record CreateCommentRequest
{
    /// <summary>A <c>CommunityContentType</c> name. <c>CommunityUpdate</c> maps to <c>post</c>.</summary>
    public string? ContentType { get; init; }

    public Guid? ContentId { get; init; }

    public required string Body { get; init; }

    /// <summary>Set to reply. The parent must be on the same content.</summary>
    public Guid? ParentCommentId { get; init; }

    /// <summary>Set to quote another comment. Different from replying to it.</summary>
    public Guid? QuotedCommentId { get; init; }

    /// <summary>A Media reference. Never a URL.</summary>
    public Guid? AttachmentMediaId { get; init; }
}
