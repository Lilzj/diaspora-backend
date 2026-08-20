using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Community;

/// <summary>
/// Somebody named a member in a post or a comment.
/// <para>
/// <b>The one community signal that is genuinely urgent.</b> A mention is somebody asking you a
/// question in public; a reaction is not. The rule gives it a higher priority than everything else
/// in the category for exactly that reason, and the difference is visible in a row rather than
/// buried here.
/// </para>
/// <para>
/// The mention targets a user directly — B6's contract carries <c>TargetType</c> and
/// <c>TargetId</c> — so only mentions of a member produce a trigger. A mention of an organization is
/// somebody else's fan-out problem.
/// </para>
/// </summary>
public sealed class CommunityMentionConsumer
    : ModuleEventConsumer<CommunityMentionCreatedIntegrationEvent>
{
    private const string UserTarget = "User";

    public CommunityMentionConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Community";

    protected override IEnumerable<NotificationTrigger> Translate(
        CommunityMentionCreatedIntegrationEvent integrationEvent)
    {
        if (!string.Equals(integrationEvent.TargetType, UserTarget, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        // Somebody mentioning themselves is not news, and it is the most common way a member would
        // otherwise be notified about their own typing.
        if (integrationEvent.TargetId == integrationEvent.MentionedBy)
        {
            yield break;
        }

        var values = Values();
        values["contentType"] = integrationEvent.ContentType;
        values["contentId"] = integrationEvent.ContentId;

        yield return Trigger(
            UserId.From(integrationEvent.TargetId),
            "mentioned",
            integrationEvent.OccurredAt,
            values,
            subjectType: integrationEvent.ContentType.ToLowerInvariant(),
            subjectId: integrationEvent.ContentId.ToString());
    }
}
