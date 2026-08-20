using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.CommunityLife;

/// <summary>
/// A membership ended, and exactly one of the three ways it can end produces a message.
/// <para>
/// <b>Lapsing is the only one the member did not already know about.</b> Somebody who walked away
/// knows they walked away, and telling them would be a platform confirming a decision they made
/// thirty seconds ago. Somebody removed by an administrator is a harder call and the answer is still
/// no here: a removal is a conversation between them and the organization, and a push notification
/// announcing it — with no explanation, because the contract carries none — is the platform picking
/// the worst possible way for them to find out.
/// </para>
/// <para>
/// <b>A lapse is different because nobody decided it.</b> A renewal window passed while they were not
/// looking, it is reversible by asking, and the alternative to telling them is that they discover it
/// the next time they try to do something they used to be able to do.
/// </para>
/// </summary>
public sealed class OrganizationMemberLeftConsumer
    : ModuleEventConsumer<OrganizationMemberLeftIntegrationEvent>
{
    public OrganizationMemberLeftConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "CommunityLife";

    protected override IEnumerable<NotificationTrigger> Translate(
        OrganizationMemberLeftIntegrationEvent integrationEvent)
    {
        if (!integrationEvent.WasLapsed)
        {
            yield break;
        }

        var values = Values();
        values["organizationId"] = integrationEvent.OrganizationId;
        values["memberCount"] = integrationEvent.MemberCount;

        yield return Trigger(
            UserId.From(integrationEvent.MemberUserId),
            "lapsed-member",
            integrationEvent.LeftAt,
            values,
            subjectType: "organization",
            subjectId: integrationEvent.OrganizationId.ToString());
    }
}
