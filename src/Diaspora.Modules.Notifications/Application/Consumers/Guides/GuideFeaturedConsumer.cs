using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Guides;

/// <summary>
/// An editor featured or unfeatured a guide.
/// <para>
/// <b>The author is told, which is the whole point, and until B13.1 this module could not do it.</b>
/// B8's event carried <c>ChangedBy</c> — the staff editor who made the call — and no author, so the
/// only message it could produce was one congratulating an editor on featuring somebody else's work.
/// <c>AuthorUserId</c> is the enrichment that turned a placeholder into the message the client's
/// mock notification list has shown since the beginning.
/// </para>
/// <para>
/// Unfeaturing produces nothing. Nobody needs a notification saying their guide is no longer on a
/// shelf.
/// </para>
/// <para>
/// Null on a guide hard-deleted before the relay read it back, and on every event published before
/// B13.1. Both mean nothing is sent, which is right: a replay must not start congratulating people
/// on curation decisions taken months ago.
/// </para>
/// </summary>
public sealed class GuideFeaturedConsumer : ModuleEventConsumer<GuideFeaturedIntegrationEvent>
{
    public GuideFeaturedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Guides";

    protected override IEnumerable<NotificationTrigger> Translate(
        GuideFeaturedIntegrationEvent integrationEvent)
    {
        if (!integrationEvent.Featured || integrationEvent.AuthorUserId is not { } authorUserId)
        {
            yield break;
        }

        var values = Values();
        values["guideId"] = integrationEvent.GuideId;
        values["featuredAt"] = integrationEvent.ChangedAt;

        yield return Trigger(
            UserId.From(authorUserId),
            "author",
            integrationEvent.ChangedAt,
            values,
            subjectType: "guide",
            subjectId: integrationEvent.GuideId.ToString());
    }
}
