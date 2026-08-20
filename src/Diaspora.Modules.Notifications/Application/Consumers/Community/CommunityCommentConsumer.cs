using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Community;

/// <summary>
/// Somebody commented, or replied to a comment.
/// <para>
/// <b>Two different people may need telling, and they need telling differently.</b> The author of
/// the thing being discussed gets "somebody commented on your post"; the author of the comment being
/// replied to gets "somebody replied to you". Two audiences, two rules, two templates — and one
/// person who is both gets one message, because the de-duplication key is the content and the two
/// triggers resolve to the same subject.
/// </para>
/// <para>
/// Self-comments produce nothing. Everybody replies to themselves in a long thread, and being
/// notified about it is how members learn to switch a category off.
/// </para>
/// </summary>
public sealed class CommunityCommentConsumer
    : ModuleEventConsumer<CommunityCommentAddedIntegrationEvent>
{
    public CommunityCommentConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Community";

    protected override IEnumerable<NotificationTrigger> Translate(
        CommunityCommentAddedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["contentType"] = integrationEvent.ContentType;
        values["contentId"] = integrationEvent.ContentId;
        values["depth"] = integrationEvent.Depth;

        var group = $"community:comments:{integrationEvent.ContentId}";

        if (integrationEvent.ParentAuthorId is { } parentAuthor
            && parentAuthor != integrationEvent.AuthorId)
        {
            yield return Trigger(
                UserId.From(parentAuthor),
                "comment-parent-author",
                integrationEvent.OccurredAt,
                values,
                subjectType: integrationEvent.ContentType.ToLowerInvariant(),
                subjectId: integrationEvent.ContentId.ToString(),
                groupKey: group);
        }

        if (integrationEvent.ContentAuthorId is not { } contentAuthor
            || contentAuthor == integrationEvent.AuthorId
            || contentAuthor == integrationEvent.ParentAuthorId)
        {
            yield break;
        }

        yield return Trigger(
            UserId.From(contentAuthor),
            "comment-content-author",
            integrationEvent.OccurredAt,
            values,
            subjectType: integrationEvent.ContentType.ToLowerInvariant(),
            subjectId: integrationEvent.ContentId.ToString(),
            groupKey: group);
    }
}
