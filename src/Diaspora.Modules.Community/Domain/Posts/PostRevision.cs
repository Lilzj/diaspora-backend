using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Posts;

/// <summary>
/// One earlier state of a post body.
/// <para>
/// Written <b>before</b> an edit is applied, holding the text as it was. That direction matters:
/// storing the new text would mean the current version exists twice and the original is lost
/// after the first edit, which is the opposite of what a revision history is for.
/// </para>
/// <para>
/// This is what serves the client's guide version screen, which renders a version number, a
/// title, a body and a list of change summaries. The change summary is author-supplied and
/// optional — B6 does not diff two bodies and invent a description of what changed, because a
/// generated summary of a human edit is usually wrong and always sounds authoritative.
/// </para>
/// </summary>
public sealed class PostRevision : Entity<PostRevisionId>
{
    private PostRevision(
        PostRevisionId id,
        CommunityPostId postId,
        int revisionNumber,
        string? title,
        string body,
        UserId editedBy,
        string? changeSummary,
        DateTimeOffset createdAt) : base(id)
    {
        PostId = postId;
        RevisionNumber = revisionNumber;
        Title = title;
        Body = body;
        EditedBy = editedBy;
        ChangeSummary = changeSummary;
        CreatedAt = createdAt;
    }

    private PostRevision() { }

    public CommunityPostId PostId { get; private set; } = default!;

    /// <summary>One-based. Revision 1 is the post as first published.</summary>
    public int RevisionNumber { get; private set; }

    public string? Title { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public UserId EditedBy { get; private set; } = default!;

    /// <summary>Author-supplied. Never generated.</summary>
    public string? ChangeSummary { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static PostRevision Capture(
        CommunityPostId postId,
        int revisionNumber,
        string? title,
        string body,
        UserId editedBy,
        string? changeSummary,
        DateTimeOffset createdAt) =>
        new(
            PostRevisionId.New(),
            postId,
            revisionNumber,
            title,
            body,
            editedBy,
            changeSummary,
            createdAt);
}
