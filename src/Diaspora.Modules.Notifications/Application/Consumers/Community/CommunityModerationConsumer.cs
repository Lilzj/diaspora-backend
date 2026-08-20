using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Community;

/// <summary>
/// A moderator decided something about a member's content.
/// <para>
/// <b>Never digested and never muted, because it is the only notice a member gets that something
/// they wrote was removed.</b> A platform that files this alongside reactions is a platform where
/// people find their posts gone and no explanation anywhere. The rule gives it a high priority in a
/// mandatory category for exactly that reason.
/// </para>
/// <para>
/// The action and the reason travel; the moderator does not. Naming the individual who made a
/// decision invites members to take it up with them personally, which is a safety problem for the
/// moderator and a policy problem for the platform.
/// </para>
/// </summary>
public sealed class CommunityModerationConsumer
    : ModuleEventConsumer<CommunityModerationDecidedIntegrationEvent>
{
    public CommunityModerationConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Community";

    protected override IEnumerable<NotificationTrigger> Translate(
        CommunityModerationDecidedIntegrationEvent integrationEvent)
    {
        if (integrationEvent.ContentAuthorId is not { } author)
        {
            yield break;
        }

        var values = Values();
        values["contentType"] = integrationEvent.ContentType;
        values["contentId"] = integrationEvent.ContentId;
        values["action"] = integrationEvent.Action;
        values["reason"] = integrationEvent.Reason ?? string.Empty;

        yield return Trigger(
            UserId.From(author),
            "content-author",
            integrationEvent.OccurredAt,
            values,
            subjectType: integrationEvent.ContentType.ToLowerInvariant(),
            subjectId: integrationEvent.ContentId.ToString());
    }
}
