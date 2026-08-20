using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Community;

/// <summary>Threading, flattening and tombstoning — the three rules a renderer must not reinvent.</summary>
public class CommentThreadTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly ContentReference Content =
        ContentReference.Create(CommunityContentType.Post, Guid.NewGuid());

    [Fact]
    public void A_top_level_comment_has_no_parent_and_depth_zero()
    {
        var comment = Create();

        comment.Depth.Should().Be(0);
        comment.ParentCommentId.Should().BeNull();
        comment.Path.Should().Be(comment.Id.Value.ToString("N"));
    }

    [Fact]
    public void A_reply_extends_its_parents_path()
    {
        var parent = Create();
        var reply = Create(parent);

        reply.Depth.Should().Be(1);
        reply.Path.Should().StartWith(parent.Path + Comment.PathSeparator);
    }

    [Fact]
    public void A_reply_on_different_content_from_its_parent_is_refused()
    {
        var parent = Create();

        var elsewhere = ContentReference.Create(CommunityContentType.Guide, Guid.NewGuid());

        Comment.Create(elsewhere, UserId.New(), Body(), parent, Now)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void The_stored_chain_is_not_capped_at_the_rendered_depth()
    {
        var current = Create();

        for (var depth = 0; depth < CommunityLimits.MaxRenderedCommentDepth + 4; depth++)
        {
            current = Create(current);
        }

        current.Depth.Should().BeGreaterThan(
            CommunityLimits.MaxRenderedCommentDepth,
            "the brief asks for unlimited nesting; only the rendering flattens");
    }

    [Fact]
    public void Beyond_the_rendered_depth_replies_flatten_but_keep_their_true_depth()
    {
        var comments = new List<Comment>();
        var current = Create();
        comments.Add(current);

        for (var depth = 0; depth < CommunityLimits.MaxRenderedCommentDepth + 3; depth++)
        {
            current = Create(current);
            comments.Add(current);
        }

        var thread = CommentThread.Assemble(Content, comments);
        var deepest = Flatten(thread.Roots).MaxBy(node => node.Comment.Depth)!;

        deepest.RenderedDepth.Should().Be(CommunityLimits.MaxRenderedCommentDepth);
        deepest.Comment.Depth.Should().BeGreaterThan(deepest.RenderedDepth);
    }

    [Fact]
    public void A_pinned_comment_sorts_first_whatever_its_timestamp()
    {
        var older = Create();
        var newer = Create(createdAt: Now.AddHours(1));
        newer.Pin(true);

        var thread = CommentThread.Assemble(Content, [older, newer]);

        thread.Roots[0].Comment.Id.Should().Be(newer.Id);
    }

    [Fact]
    public void An_orphaned_reply_is_adopted_at_the_root_rather_than_dropped()
    {
        var parent = Create();
        var orphan = Create(parent);

        // Only the reply is supplied — the parent fell outside the page.
        var thread = CommentThread.Assemble(Content, [orphan]);

        thread.Roots.Should().ContainSingle().Which.Comment.Id.Should().Be(orphan.Id);
    }

    [Fact]
    public void A_deleted_comment_is_tombstoned_rather_than_erased()
    {
        var comment = Create();

        comment.MarkDeleted(Now, null);

        comment.IsTombstoned.Should().BeTrue();
        comment.Status.Should().Be(CommentStatus.Deleted);
        comment.Body.Value.Should().NotBeEmpty("the row survives so the thread keeps its shape");
    }

    [Fact]
    public void A_reported_comment_returns_to_active_when_its_reports_clear()
    {
        var comment = Create();

        comment.AdjustReports(1);
        comment.Status.Should().Be(CommentStatus.Reported);

        comment.AdjustReports(-1);
        comment.Status.Should().Be(CommentStatus.Active);
    }

    [Fact]
    public void Editing_a_comment_always_marks_it_edited()
    {
        var comment = Create();

        comment.Edit(Body("Rewritten"), Now.AddSeconds(5));

        comment.IsEdited.Should().BeTrue(
            "a comment is a turn in a conversation, and a reader needs to know it changed");
    }

    private static IEnumerable<CommentNode> Flatten(IEnumerable<CommentNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in Flatten(node.Replies))
            {
                yield return child;
            }
        }
    }

    private static Comment Create(Comment? parent = null, DateTimeOffset? createdAt = null) =>
        Comment.Create(Content, UserId.New(), Body(), parent, createdAt ?? Now).Value;

    private static CommentBody Body(string value = "Useful, thank you.") =>
        CommentBody.Create(value).Value;
}
