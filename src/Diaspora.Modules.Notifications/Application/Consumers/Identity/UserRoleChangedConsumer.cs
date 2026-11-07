using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Identity;

/// <summary>
/// A member's platform role changed (7 Nov 2026).
/// <para>
/// Until then nobody was told: a member made a Moderator, or stripped of being one, found out from
/// what the app let them do. The member now reads their new role and the reason staff gave. The
/// rule makes it Critical, so it reaches email whatever the member's settings say, like every other
/// change staff make to an account.
/// </para>
/// </summary>
public sealed class UserRoleChangedConsumer : ModuleEventConsumer<UserRoleChangedIntegrationEvent>
{
    public UserRoleChangedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Identity";

    protected override IEnumerable<NotificationTrigger> Translate(
        UserRoleChangedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["previousrole"] = Describe(integrationEvent.PreviousRole);
        values["newrole"] = Describe(integrationEvent.NewRole);
        values["reasontext"] = string.IsNullOrWhiteSpace(integrationEvent.Reason)
            ? string.Empty
            : $"The reason Diaspora gave: {integrationEvent.Reason.Trim().TrimEnd('.')}. ";

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.OccurredAt,
            values,
            subjectType: "account",
            subjectId: integrationEvent.UserId.ToString());
    }

    /// <summary>The role in the words a member reads, with its article, not the platform's identifier.</summary>
    private static string Describe(string role) => role switch
    {
        "Guest" => "a guest",
        "CommunityUser" => "a member",
        "ServiceProvider" => "a service provider",
        "Moderator" => "a moderator",
        "Admin" => "an administrator",
        "SuperAdmin" => "a super administrator",
        _ => role
    };
}
