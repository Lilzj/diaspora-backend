using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Community;

/// <summary>
/// Somebody reacted to a member's content.
/// <para>
/// <b>The brief's own example of what a digest is for.</b> A single reaction is not worth
/// interrupting anybody; forty of them in an evening is worth one line tomorrow morning. The rule
/// gives it Low priority and a digest policy, and the group key is the content, so the digest can
/// collapse them into "twelve people reacted to your post".
/// </para>
/// <para>
/// A negative reaction produces no notification at all. Telling somebody a stranger disliked their
/// post serves nobody, and the sentiment is on the contract precisely so this decision is possible.
/// </para>
/// </summary>
public sealed class CommunityReactionConsumer
    : ModuleEventConsumer<CommunityReactionAddedIntegrationEvent>
{
    private const string NegativeSentiment = "Negative";

    public CommunityReactionConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Community";

    protected override IEnumerable<NotificationTrigger> Translate(
        CommunityReactionAddedIntegrationEvent integrationEvent)
    {
        if (integrationEvent.ContentAuthorId is not { } author
            || author == integrationEvent.UserId
            || string.Equals(
                integrationEvent.Sentiment, NegativeSentiment, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var values = Values();
        values["contentType"] = integrationEvent.ContentType;
        values["contentId"] = integrationEvent.ContentId;
        values["reactionKey"] = integrationEvent.ReactionKey;

        yield return Trigger(
            UserId.From(author),
            "content-author",
            integrationEvent.OccurredAt,
            values,
            subjectType: integrationEvent.ContentType.ToLowerInvariant(),
            subjectId: integrationEvent.ContentId.ToString(),
            groupKey: $"community:reactions:{integrationEvent.ContentId}");
    }
}
