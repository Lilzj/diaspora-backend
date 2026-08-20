using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.CommunityLife;

/// <summary>
/// Somebody joined an organization.
/// <para>
/// The brief's "membership approved". The member is told; the organization's leaders are not,
/// because the contract names the joiner and no one else — and a body with forty thousand members
/// notifying its committee on every join would be a fan-out this module cannot bound.
/// </para>
/// </summary>
public sealed class OrganizationMemberJoinedConsumer
    : ModuleEventConsumer<OrganizationMemberJoinedIntegrationEvent>
{
    public OrganizationMemberJoinedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "CommunityLife";

    protected override IEnumerable<NotificationTrigger> Translate(
        OrganizationMemberJoinedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["role"] = integrationEvent.Role;
        values["memberCount"] = integrationEvent.MemberCount;
        values["organizationId"] = integrationEvent.OrganizationId;

        yield return Trigger(
            UserId.From(integrationEvent.MemberUserId),
            "member",
            integrationEvent.JoinedAt,
            values,
            subjectType: "organization",
            subjectId: integrationEvent.OrganizationId.ToString());
    }
}
